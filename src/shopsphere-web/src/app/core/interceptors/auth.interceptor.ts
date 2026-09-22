import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  if (!req.url.startsWith('/api/')) {
    return next(req);
  }

  // login/register/refresh/logout must never trigger another refresh
  const isAuthEndpoint = req.url.startsWith('/api/auth/') && !req.url.endsWith('/me');

  return next(withToken(req, auth.token)).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status !== 401 || isAuthEndpoint || !auth.token) {
        return throwError(() => error);
      }

      return auth.refresh().pipe(
        switchMap(() => next(withToken(req, auth.token))),
        catchError(() => {
          auth.sessionExpired();
          return throwError(() => error);
        })
      );
    })
  );
};

function withToken(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
}
