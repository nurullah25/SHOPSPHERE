import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { provideNativeDateAdapter } from '@angular/material/core';
import { finalize } from 'rxjs';
import { AdminInsightsService } from '../../services/admin-insights.service';
import { AdminCoupon } from '../../../core/models/admin-insights.models';
import { getErrorMessage } from '../../../core/http/error-message';

@Component({
  selector: 'app-coupon-dialog',
  providers: [provideNativeDateAdapter()],
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule
  ],
  templateUrl: './coupon-dialog.html',
  styleUrl: './coupon-dialog.scss'
})
export class CouponDialog {
  private readonly insights = inject(AdminInsightsService);
  private readonly dialogRef = inject(MatDialogRef<CouponDialog, AdminCoupon>);
  private readonly fb = inject(FormBuilder);

  protected readonly coupon = inject<AdminCoupon | null>(MAT_DIALOG_DATA);
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    code: [this.coupon?.code ?? '', [Validators.required, Validators.pattern(/^[A-Za-z0-9-]+$/)]],
    description: [this.coupon?.description ?? ''],
    discountType: [this.coupon?.discountType ?? ('Percentage' as 'Percentage' | 'FixedAmount')],
    discountValue: [this.coupon?.discountValue ?? 10, [Validators.required, Validators.min(0.01)]],
    minOrderAmount: [this.coupon?.minOrderAmount ?? null as number | null],
    maxDiscountAmount: [this.coupon?.maxDiscountAmount ?? null as number | null],
    startsAt: [this.coupon?.startsAt ? new Date(this.coupon.startsAt) : null as Date | null],
    expiresAt: [this.coupon?.expiresAt ? new Date(this.coupon.expiresAt) : null as Date | null],
    usageLimit: [this.coupon?.usageLimit ?? null as number | null],
    usageLimitPerCustomer: [this.coupon?.usageLimitPerCustomer ?? null as number | null],
    isActive: [this.coupon?.isActive ?? true]
  });

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const request = {
      code: value.code.trim().toUpperCase(),
      description: value.description.trim() || null,
      discountType: value.discountType,
      discountValue: value.discountValue,
      minOrderAmount: value.minOrderAmount,
      maxDiscountAmount: value.maxDiscountAmount,
      startsAt: value.startsAt ? value.startsAt.toISOString() : null,
      expiresAt: value.expiresAt ? value.expiresAt.toISOString() : null,
      usageLimit: value.usageLimit,
      usageLimitPerCustomer: value.usageLimitPerCustomer,
      isActive: value.isActive
    };

    this.saving.set(true);
    this.errorMessage.set(null);

    const save$ = this.coupon
      ? this.insights.updateCoupon(this.coupon.id, request)
      : this.insights.createCoupon(request);

    save$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: coupon => this.dialogRef.close(coupon),
      error: error => this.errorMessage.set(getErrorMessage(error))
    });
  }
}
