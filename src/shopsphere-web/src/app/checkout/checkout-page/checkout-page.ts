import { Component, computed, inject, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import { finalize } from 'rxjs';
import { CheckoutService } from '../checkout.service';
import { CartService } from '../../cart/cart.service';
import { CheckoutSummary, PAYMENT_METHODS, PlaceOrderRequest } from '../../core/models/checkout.models';
import { getErrorMessage } from '../../core/http/error-message';
import { EmptyState } from '../../shared/components/empty-state/empty-state';

const NEW_ADDRESS = 0;

@Component({
  selector: 'app-checkout-page',
  imports: [
    CurrencyPipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatRadioModule,
    EmptyState
  ],
  templateUrl: './checkout-page.html',
  styleUrl: './checkout-page.scss'
})
export class CheckoutPage {
  private readonly checkout = inject(CheckoutService);
  private readonly cart = inject(CartService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  protected readonly paymentMethods = PAYMENT_METHODS;
  protected readonly newAddressId = NEW_ADDRESS;

  protected readonly summary = signal<CheckoutSummary | null>(null);
  protected readonly loading = signal(true);
  protected readonly placingOrder = signal(false);
  protected readonly couponPending = signal(false);
  protected readonly couponError = signal<string | null>(null);
  protected readonly orderError = signal<string | null>(null);

  protected readonly addressForm = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(200)]],
    line1: ['', [Validators.required, Validators.maxLength(200)]],
    line2: [''],
    city: ['', [Validators.required, Validators.maxLength(100)]],
    state: [''],
    postalCode: ['', [Validators.required, Validators.maxLength(20)]],
    country: ['United States', [Validators.required, Validators.maxLength(100)]],
    phoneNumber: [''],
    saveToAddressBook: [true]
  });

  protected readonly checkoutForm = this.fb.nonNullable.group({
    addressId: [NEW_ADDRESS],
    paymentToken: [PAYMENT_METHODS[0].token, Validators.required],
    couponCode: [''],
    notes: ['']
  });

  protected readonly usingNewAddress = computed(() => this.selectedAddressId() === NEW_ADDRESS);
  private readonly selectedAddressId = signal(NEW_ADDRESS);

  constructor() {
    this.checkoutForm.controls.addressId.valueChanges.subscribe(value => this.selectedAddressId.set(value));
    this.loadSummary();
  }

  applyCoupon(): void {
    const code = this.checkoutForm.controls.couponCode.value.trim();
    if (!code) return;

    this.couponPending.set(true);
    this.couponError.set(null);

    this.checkout
      .validateCoupon(code)
      .pipe(finalize(() => this.couponPending.set(false)))
      .subscribe({
        next: summary => this.summary.set(summary),
        error: error => this.couponError.set(getErrorMessage(error))
      });
  }

  removeCoupon(): void {
    this.checkoutForm.controls.couponCode.setValue('');
    this.couponError.set(null);
    this.loadSummary();
  }

  placeOrder(): void {
    const useNewAddress = this.usingNewAddress();

    if (useNewAddress && this.addressForm.invalid) {
      this.addressForm.markAllAsTouched();
      return;
    }

    const value = this.checkoutForm.getRawValue();
    const address = this.addressForm.getRawValue();

    const request: PlaceOrderRequest = {
      couponCode: this.summary()?.couponCode ?? null,
      addressId: useNewAddress ? null : value.addressId,
      shippingAddress: useNewAddress
        ? {
            fullName: address.fullName,
            line1: address.line1,
            line2: address.line2 || null,
            city: address.city,
            state: address.state || null,
            postalCode: address.postalCode,
            country: address.country,
            phoneNumber: address.phoneNumber || null,
            saveToAddressBook: address.saveToAddressBook
          }
        : null,
      paymentToken: value.paymentToken,
      notes: value.notes.trim() || null
    };

    this.placingOrder.set(true);
    this.orderError.set(null);

    this.checkout
      .placeOrder(request)
      .pipe(finalize(() => this.placingOrder.set(false)))
      .subscribe({
        next: result => {
          this.cart.refresh();
          this.router.navigate(['/checkout/success', result.orderNumber]);
        },
        error: error => {
          this.orderError.set(getErrorMessage(error));
          // Stock or coupon state may have changed, so reload the totals
          this.loadSummary();
        }
      });
  }

  private loadSummary(): void {
    const coupon = this.summary()?.couponCode ?? null;

    this.checkout
      .getSummary(coupon)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: summary => {
          this.summary.set(summary);
          this.prefillAddress(summary);
        },
        error: () => this.summary.set(null)
      });
  }

  private prefillAddress(summary: CheckoutSummary): void {
    const preferred = summary.savedAddresses.find(address => address.isDefault) ?? summary.savedAddresses[0];
    if (preferred) {
      this.checkoutForm.controls.addressId.setValue(preferred.id);
    }
  }
}
