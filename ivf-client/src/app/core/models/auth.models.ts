export interface LoginRequest {
  username: string;
  password: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  user: User;
}

export interface MfaRequiredResponse {
  code: 'MFA_REQUIRED';
  mfaToken: string;
  mfaMethod: string;
  user: { id: string; username: string; fullName: string; role: string };
}

export interface LoginErrorResponse {
  error: string;
  code: string;
  reason?: string;
  unlocksAt?: string;
  failedAttempts?: number;
}

export interface MfaVerifyRequest {
  mfaToken: string;
  code: string;
}

export interface User {
  id: string;
  username: string;
  fullName: string;
  role: string;
  department?: string;
  isPlatformAdmin?: boolean;
  tenantId?: string;
}
