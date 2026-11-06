import { startSession } from '@/features/auth/session';
import type { TokenResponse, UserRole } from '@/features/auth/types';

export function makeTokens(role: UserRole, overrides: Partial<TokenResponse> = {}): TokenResponse {
  return {
    accessToken: 'test-access-token',
    accessTokenExpiresAt: '2030-01-01T00:00:00Z',
    refreshToken: 'test-refresh-token',
    refreshTokenExpiresAt: '2030-01-01T00:00:00Z',
    user: { id: 'u-1', email: `${role.toLowerCase()}@example.test`, displayName: `${role} User`, role },
    ...overrides,
  };
}

/** Puts the app in a signed-in state without any network call. Call resetSession() in afterEach. */
export async function signInAs(role: UserRole, overrides: Partial<TokenResponse> = {}): Promise<void> {
  await startSession(makeTokens(role, overrides));
}
