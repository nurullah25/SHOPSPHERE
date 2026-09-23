import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, catchError, finalize, map, of, shareReplay, tap, throwError } from 'rxjs';
import { silentErrors } from '../http/http-context';
import { AuthResponse, AuthUser, LoginRequest, RegisterRequest } from '../models/auth.models';
import { NotificationService } from '../services/notification.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);

  private readonly currentUserSubject = new BehaviorSubject<AuthUser | null>(null);

  // Kept in memory only. The refresh token lives in an HttpOnly cookie,
  // so a page reload gets a new access token through restoreSession().
  private accessToken: string | null = null;
  private refreshRequest$: Observable<AuthResponse> | null = null;

  readonly currentUser$ = this.currentUserSubject.asObservable();
  readonly isLoggedIn$ = this.currentUser$.pipe(map(user => user !== null));

  get currentUser(): AuthUser | null {
    return this.currentUserSubject.value;
  }

  get token(): string | null {
    return this.accessToken;
  }

  isAdmin(): boolean {
    return this.currentUser?.role === 'Admin';
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>('/api/auth/login', request, { context: silentErrors() })
      .pipe(tap(response => this.setSession(response)));
  }

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>('/api/auth/register', request, { context: silentErrors() })
      .pipe(tap(response => this.setSession(response)));
  }

  // Several requests can fail with 401 at the same time. They all share one
  // refresh call instead of each rotating the refresh token.
  refresh(): Observable<AuthResponse> {
    if (!this.refreshRequest$) {
      this.refreshRequest$ = this.http
        .post<AuthResponse>('/api/auth/refresh', null, { context: silentErrors() })
        .pipe(
          tap(response => this.setSession(response)),
          catchError(error => {
            this.clearSession();
            return throwError(() => error);
          }),
          finalize(() => (this.refreshRequest$ = null)),
          shareReplay(1)
        );
    }
    return this.refreshRequest$;
  }

  // Called after the customer edits their profile so the header stays correct
  refreshCurrentUser(): void {
    this.http
      .get<AuthUser>('/api/auth/me', { context: silentErrors() })
      .pipe(catchError(() => of(null)))
      .subscribe(user => {
        if (user) {
          this.currentUserSubject.next(user);
        }
      });
  }

  restoreSession(): Observable<unknown> {
    return this.refresh().pipe(catchError(() => of(null)));
  }

  logout(): void {
    this.http
      .post('/api/auth/logout', null, { context: silentErrors() })
      .pipe(catchError(() => of(null)))
      .subscribe();

    this.clearSession();
    this.router.navigateByUrl('/');
  }

  sessionExpired(): void {
    this.clearSession();
    this.notifications.error('Your session has expired. Please sign in again.');
    this.router.navigate(['/login'], { queryParams: { returnUrl: this.router.url } });
  }

  private setSession(response: AuthResponse): void {
    this.accessToken = response.accessToken;
    this.currentUserSubject.next(response.user);
  }

  private clearSession(): void {
    this.accessToken = null;
    this.currentUserSubject.next(null);
  }
}
