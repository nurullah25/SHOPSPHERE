import { Component, inject } from '@angular/core';
import { AsyncPipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { AuthService } from '../../auth/auth.service';

@Component({
  selector: 'app-header',
  imports: [
    AsyncPipe,
    ReactiveFormsModule,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatDividerModule
  ],
  templateUrl: './header.html',
  styleUrl: './header.scss'
})
export class Header {
  private readonly router = inject(Router);

  protected readonly auth = inject(AuthService);
  protected readonly user$ = this.auth.currentUser$;
  protected readonly search = new FormControl('', { nonNullable: true });

  submitSearch(): void {
    const term = this.search.value.trim();
    this.router.navigate(['/products'], { queryParams: { search: term || null } });
  }
}
