import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTabsModule } from '@angular/material/tabs';
import { filter, finalize, switchMap } from 'rxjs';
import { AccountService, Address, Profile } from '../account.service';
import { AddressDialog } from '../address-dialog/address-dialog';
import { AuthService } from '../../core/auth/auth.service';
import { ConfirmDialogService } from '../../shared/components/confirm-dialog/confirm-dialog';
import { getErrorMessage } from '../../core/http/error-message';
import { NotificationService } from '../../core/services/notification.service';
import { PASSWORD_PATTERN, passwordMatchValidator } from '../../shared/validators/password-match.validator';

@Component({
  selector: 'app-account-page',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatTabsModule
  ],
  templateUrl: './account-page.html',
  styleUrl: './account-page.scss'
})
export class AccountPage {
  private readonly account = inject(AccountService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly notifications = inject(NotificationService);
  private readonly fb = inject(FormBuilder);

  protected readonly profile = signal<Profile | null>(null);
  protected readonly addresses = signal<Address[]>([]);
  protected readonly savingProfile = signal(false);
  protected readonly savingPassword = signal(false);
  protected readonly profileError = signal<string | null>(null);
  protected readonly passwordError = signal<string | null>(null);

  protected readonly profileForm = this.fb.nonNullable.group({
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    phoneNumber: ['']
  });

  protected readonly passwordForm = this.fb.nonNullable.group(
    {
      currentPassword: ['', Validators.required],
      newPassword: ['', [Validators.required, Validators.minLength(8), Validators.pattern(PASSWORD_PATTERN)]],
      confirmPassword: ['', Validators.required]
    },
    { validators: passwordMatchValidator('newPassword', 'confirmPassword') }
  );

  constructor() {
    this.loadProfile();
    this.loadAddresses();
  }

  saveProfile(): void {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }

    const value = this.profileForm.getRawValue();

    this.savingProfile.set(true);
    this.profileError.set(null);

    this.account
      .updateProfile({
        firstName: value.firstName,
        lastName: value.lastName,
        phoneNumber: value.phoneNumber.trim() || null
      })
      .pipe(finalize(() => this.savingProfile.set(false)))
      .subscribe({
        next: profile => {
          this.profile.set(profile);
          this.profileForm.markAsPristine();
          this.notifications.success('Your details were saved.');
          // The header greets the customer by name, so refresh the session user
          this.auth.refreshCurrentUser();
        },
        error: error => this.profileError.set(getErrorMessage(error))
      });
  }

  changePassword(): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    const value = this.passwordForm.getRawValue();

    this.savingPassword.set(true);
    this.passwordError.set(null);

    this.account
      .changePassword({ currentPassword: value.currentPassword, newPassword: value.newPassword })
      .pipe(finalize(() => this.savingPassword.set(false)))
      .subscribe({
        next: () => {
          this.passwordForm.reset();
          this.notifications.success('Your password was changed. Other devices have been signed out.');
        },
        error: error => this.passwordError.set(getErrorMessage(error))
      });
  }

  openAddressDialog(address: Address | null): void {
    this.dialog
      .open(AddressDialog, { data: address, width: '520px' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => {
        this.notifications.success(address ? 'Address updated.' : 'Address added.');
        this.loadAddresses();
      });
  }

  deleteAddress(address: Address): void {
    this.confirmDialog
      .confirm({
        title: 'Delete this address?',
        message: 'Orders already placed keep the address they were shipped to.',
        confirmText: 'Delete',
        destructive: true
      })
      .pipe(
        filter(Boolean),
        switchMap(() => this.account.deleteAddress(address.id))
      )
      .subscribe(() => {
        this.notifications.success('Address deleted.');
        this.loadAddresses();
      });
  }

  makeDefault(address: Address): void {
    this.account
      .updateAddress(address.id, { ...address, isDefault: true })
      .subscribe(() => this.loadAddresses());
  }

  private loadProfile(): void {
    this.account.getProfile().subscribe(profile => {
      this.profile.set(profile);
      this.profileForm.patchValue({
        firstName: profile.firstName,
        lastName: profile.lastName,
        phoneNumber: profile.phoneNumber ?? ''
      });
      this.profileForm.markAsPristine();
    });
  }

  private loadAddresses(): void {
    this.account.getAddresses().subscribe(addresses => this.addresses.set(addresses));
  }
}
