import { apiSend } from '@/lib/api-client';
import { refreshTokenTransport } from '@/lib/platform';

import type { LoginRequest, PasswordResetConfirmRequest, RegisterRequest, TokenResponse } from './types';

const AUTH = '/api/v1/auth';

export function register(body: RegisterRequest): Promise<TokenResponse> {
  return apiSend<TokenResponse>('POST', `${AUTH}/register`, {
    body,
    auth: 'none',
    refreshTransport: refreshTokenTransport(),
  });
}

export function login(body: LoginRequest): Promise<TokenResponse> {
  return apiSend<TokenResponse>('POST', `${AUTH}/login`, {
    body,
    auth: 'none',
    refreshTransport: refreshTokenTransport(),
  });
}

/** Native sends the stored token in the body; web sends nothing and the cookie rides along. */
export function refresh(refreshToken: string | null): Promise<TokenResponse> {
  return apiSend<TokenResponse>('POST', `${AUTH}/refresh`, {
    body: refreshToken ? { refreshToken } : {},
    auth: 'none',
    refreshTransport: refreshTokenTransport(),
  });
}

export function logout(refreshToken: string | null): Promise<void> {
  return apiSend<void>('POST', `${AUTH}/logout`, {
    body: refreshToken ? { refreshToken } : {},
    auth: 'none',
    refreshTransport: refreshTokenTransport(),
  });
}

export function requestPasswordReset(email: string): Promise<void> {
  return apiSend<void>('POST', `${AUTH}/password-reset`, { body: { email }, auth: 'none' });
}

export function confirmPasswordReset(body: PasswordResetConfirmRequest): Promise<void> {
  return apiSend<void>('POST', `${AUTH}/password-reset/confirm`, { body, auth: 'none' });
}
