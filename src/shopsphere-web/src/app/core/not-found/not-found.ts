import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-not-found',
  imports: [RouterLink, MatButtonModule],
  template: `
    <div class="page-container not-found">
      <h1>Page not found</h1>
      <p>The page you are looking for doesn't exist or has been moved.</p>
      <a mat-flat-button routerLink="/">Back to home</a>
    </div>
  `,
  styles: `
    .not-found {
      text-align: center;
      padding-top: 64px;
    }
  `
})
export class NotFound {}
