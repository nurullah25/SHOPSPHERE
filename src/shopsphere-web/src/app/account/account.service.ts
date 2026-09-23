import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { silentErrors } from '../core/http/http-context';

export interface Profile {
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber: string | null;
  createdAt: string;
}

export interface UpdateProfileRequest {
  firstName: string;
  lastName: string;
  phoneNumber: string | null;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface Address {
  id: number;
  fullName: string;
  line1: string;
  line2: string | null;
  city: string;
  state: string | null;
  postalCode: string;
  country: string;
  phoneNumber: string | null;
  isDefault: boolean;
}

export type AddressRequest = Omit<Address, 'id'>;

@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly http = inject(HttpClient);

  getProfile(): Observable<Profile> {
    return this.http.get<Profile>('/api/account/profile');
  }

  updateProfile(request: UpdateProfileRequest): Observable<Profile> {
    return this.http.put<Profile>('/api/account/profile', request, { context: silentErrors() });
  }

  changePassword(request: ChangePasswordRequest): Observable<void> {
    return this.http.put<void>('/api/account/password', request, { context: silentErrors() });
  }

  getAddresses(): Observable<Address[]> {
    return this.http.get<Address[]>('/api/account/addresses');
  }

  createAddress(request: AddressRequest): Observable<Address> {
    return this.http.post<Address>('/api/account/addresses', request, { context: silentErrors() });
  }

  updateAddress(id: number, request: AddressRequest): Observable<Address> {
    return this.http.put<Address>(`/api/account/addresses/${id}`, request, { context: silentErrors() });
  }

  deleteAddress(id: number): Observable<void> {
    return this.http.delete<void>(`/api/account/addresses/${id}`);
  }
}
