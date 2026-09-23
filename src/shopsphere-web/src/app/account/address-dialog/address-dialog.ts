import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { finalize } from 'rxjs';
import { AccountService, Address } from '../account.service';
import { getErrorMessage } from '../../core/http/error-message';

@Component({
  selector: 'app-address-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatButtonModule, MatCheckboxModule, MatFormFieldModule, MatInputModule],
  templateUrl: './address-dialog.html',
  styleUrl: './address-dialog.scss'
})
export class AddressDialog {
  private readonly account = inject(AccountService);
  private readonly dialogRef = inject(MatDialogRef<AddressDialog, Address>);
  private readonly fb = inject(FormBuilder);

  protected readonly address = inject<Address | null>(MAT_DIALOG_DATA);
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    fullName: [this.address?.fullName ?? '', [Validators.required, Validators.maxLength(200)]],
    line1: [this.address?.line1 ?? '', [Validators.required, Validators.maxLength(200)]],
    line2: [this.address?.line2 ?? ''],
    city: [this.address?.city ?? '', [Validators.required, Validators.maxLength(100)]],
    state: [this.address?.state ?? ''],
    postalCode: [this.address?.postalCode ?? '', [Validators.required, Validators.maxLength(20)]],
    country: [this.address?.country ?? 'United States', [Validators.required, Validators.maxLength(100)]],
    phoneNumber: [this.address?.phoneNumber ?? ''],
    isDefault: [this.address?.isDefault ?? false]
  });

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const request = {
      fullName: value.fullName,
      line1: value.line1,
      line2: value.line2 || null,
      city: value.city,
      state: value.state || null,
      postalCode: value.postalCode,
      country: value.country,
      phoneNumber: value.phoneNumber || null,
      isDefault: value.isDefault
    };

    this.saving.set(true);
    this.errorMessage.set(null);

    const save$ = this.address
      ? this.account.updateAddress(this.address.id, request)
      : this.account.createAddress(request);

    save$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: address => this.dialogRef.close(address),
      error: error => this.errorMessage.set(getErrorMessage(error))
    });
  }
}
