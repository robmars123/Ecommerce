// Wire types for the auth API (architecture 9, Slice 1b).

export type UserRole = 'Customer' | 'Support' | 'Admin';

export type SessionUser = { id: string; email: string; displayName: string; role: UserRole };

/** refreshToken is null when the web cookie transport was requested. */
export type TokenResponse = {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string | null;
  refreshTokenExpiresAt: string;
  user: SessionUser;
};

export type RegisterRequest = { email: string; password: string; displayName: string };
export type LoginRequest = { email: string; password: string };
export type PasswordResetConfirmRequest = { token: string; newPassword: string };
