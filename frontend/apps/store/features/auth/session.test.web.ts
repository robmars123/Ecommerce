/**
 * Runs in the web Jest project only. Moved from session.test.ts, where two cases faked Platform.OS = 'web'; the platform is
 * now chosen by file (lib/platform.web.tsx). Node environment: jsdom has no fetch or Response.
 * @jest-environment node
 */
import * as SecureStore from 'expo-secure-store';

import { jsonResponse } from '@/tests/fetch-mock';

import { endSession, getSessionSnapshot, resetSession, restoreSession, startSession } from './session';
import type { TokenResponse } from './types';

jest.mock('expo-secure-store');

const store = SecureStore as jest.Mocked<typeof SecureStore>;

function tokens(overrides: Partial<TokenResponse> = {}): TokenResponse {
  return {
    accessToken: 'access-1',
    accessTokenExpiresAt: '2030-01-01T00:00:00Z',
    refreshToken: 'refresh-1',
    refreshTokenExpiresAt: '2030-01-01T00:00:00Z',
    user: { id: 'u-1', email: 'a@example.test', displayName: 'A', role: 'Customer' },
    ...overrides,
  };
}

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
  resetSession();
  jest.clearAllMocks();
  store.getItemAsync.mockResolvedValue(null);
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

describe('session (web)', () => {
  it('on web sign-in never touches SecureStore', async () => {
    await startSession(tokens({ refreshToken: null }));
    await endSession();

    expect(store.setItemAsync).not.toHaveBeenCalled();
    expect(store.getItemAsync).not.toHaveBeenCalled();
    expect(store.deleteItemAsync).not.toHaveBeenCalled();
  });

  it('restore on web makes exactly one cookie refresh', async () => {
    const spy = jest
      .spyOn(globalThis, 'fetch')
      .mockImplementation(async () => jsonResponse(200, tokens({ refreshToken: null })));

    await restoreSession();

    expect(spy).toHaveBeenCalledTimes(1);
    expect((spy.mock.calls[0][1] as RequestInit).credentials).toBe('include');
    expect(getSessionSnapshot().status).toBe('signed-in');
  });
});
