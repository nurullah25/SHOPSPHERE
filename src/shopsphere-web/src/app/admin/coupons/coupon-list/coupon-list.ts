import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { filter, finalize, switchMap } from 'rxjs';
import { AdminInsightsService } from '../../services/admin-insights.service';
import { AdminCoupon } from '../../../core/models/admin-insights.models';
import { ConfirmDialogService } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { CouponDialog } from '../coupon-dialog/coupon-dialog';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-coupon-list',
  imports: [
    CurrencyPipe,
    DatePipe,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatTooltipModule,
    EmptyState
  ],
  templateUrl: './coupon-list.html',
  styleUrl: './coupon-list.scss'
})
export class CouponList {
  private readonly insights = inject(AdminInsightsService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly notifications = inject(NotificationService);

  protected readonly columns = ['code', 'discount', 'conditions', 'validity', 'usage', 'status', 'actions'];
  protected readonly coupons = signal<AdminCoupon[]>([]);
  protected readonly loading = signal(true);

  constructor() {
    this.load();
  }

  openDialog(coupon: AdminCoupon | null): void {
    this.dialog
      .open(CouponDialog, { data: coupon, width: '560px' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => {
        this.notifications.success(coupon ? 'Coupon saved.' : 'Coupon created.');
        this.load();
      });
  }

  deleteCoupon(coupon: AdminCoupon): void {
    this.confirmDialog
      .confirm({
        title: `Delete ${coupon.code}?`,
        message: 'Coupons that were already used on orders cannot be deleted, only deactivated.',
        confirmText: 'Delete',
        destructive: true
      })
      .pipe(
        filter(Boolean),
        switchMap(() => this.insights.deleteCoupon(coupon.id))
      )
      .subscribe(() => {
        this.notifications.success(`${coupon.code} was deleted.`);
        this.load();
      });
  }

  protected isExpired(coupon: AdminCoupon): boolean {
    return coupon.expiresAt !== null && new Date(coupon.expiresAt) <= new Date();
  }

  private load(): void {
    this.loading.set(true);
    this.insights
      .getCoupons()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: coupons => this.coupons.set(coupons),
        error: () => this.coupons.set([])
      });
  }
}
