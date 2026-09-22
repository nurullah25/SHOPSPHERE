import { Component, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-admin-dashboard',
  template: `
    <h1>Dashboard</h1>
    <p>Welcome back, {{ auth.currentUser?.firstName }}.</p>
  `
})
export class Dashboard {
  protected readonly auth = inject(AuthService);
}
