export interface AuthUser {
  id: number;
  email: string;
  firstName: string;
  lastName: string;
  role: 'Customer' | 'Admin';
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: AuthUser;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
}
