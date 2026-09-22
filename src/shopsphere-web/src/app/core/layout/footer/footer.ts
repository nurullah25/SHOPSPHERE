import { Component } from '@angular/core';

@Component({
  selector: 'app-footer',
  template: `
    <footer class="footer">
      <span>&copy; {{ year }} ShopSphere</span>
      <span>Demo store &middot; no real payments are processed</span>
    </footer>
  `,
  styles: `
    .footer {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      gap: 8px;
      padding: 20px 16px;
      background-color: var(--mat-sys-surface-container);
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `
})
export class Footer {
  protected readonly year = new Date().getFullYear();
}
