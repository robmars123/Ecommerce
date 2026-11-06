import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

import { jsonResponse, problemResponse } from '@/tests/fetch-mock';

import {
  endSession,
  getAccessToken,
  getSessionSnapshot,
  refreshSession,
  resetSession,
  restoreSession,
  setOnSignedOut,
  startSession,
} from './session';
import type { TokenResponse } from './types';

jest.mock('expo-secure-store');

const store = SecureStore as jest.Mocked<typeof SecureStore>;
const platform = Platform as { OS: string };
const ORIGINAL_OS = Platform.OS;

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
  platform.OS = 'ios';
  resetSession();
  jest.clearAllMocks();
  store.getItemAsync.mockResolvedValue(null);
});

afterEach(() => {
  platform.OS = ORIGINAL_OS;
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

describe('session', () => {
  it('starts restoring and a native sign-in stores the refresh token with the device-only keychain option', async () => {
    expect(getSessionSnapshot().status).toBe('restoring');

    await startSession(tokens());

    expect(getSessionSnapshot()).toEqual({ status: 'signed-in', user: tokens().user });
    expect(store.setItemAsync).toHaveBeenCalledWith('ecommerce.refreshToken', 'refresh-1', {
      keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
    });
  });

  it('never puts a token in the snapshot', async () => {
    await startSession(tokens());

    const text = JSON.stringify(getSessionSnapshot());
    expect(text).not.toContain('access-1');
    expect(text).not.toContain('refresh-1');
    expect(getAccessToken()).toBe('access-1');
  });

  it('on web sign-in never touches SecureStore', async () => {
    platform.OS = 'web';

    await startSession(tokens({ refreshToken: null }));
    await endSession();

    expect(store.setItemAsync).not.toHaveBeenCalled();
    expect(store.getItemAsync).not.toHaveBeenCalled();
    expect(store.deleteItemAsync).not.toHaveBeenCalled();
  });

  it('concurrent refreshes share one request (single-flight) and save the rotated token', async () => {
    store.getItemAsync.mockResolvedValue('refresh-1');
    const spy = jest
      .spyOn(globalThis, 'fetch')
      .mockImplementation(async () => jsonResponse(200, tokens({ accessToken: 'access-2', refreshToken: 'refresh-2' })));

    const results = await Promise.all([refreshSession(), refreshSession(), refreshSession()]);

    expect(results).toEqual([true, true, true]);
    expect(spy).toHaveBeenCalledTimes(1);
    expect(JSON.parse((spy.mock.calls[0][1] as RequestInit).body as string)).toEqual({ refreshToken: 'refresh-1' });
    expect(getAccessToken()).toBe('access-2');
    expect(store.setItemAsync).toHaveBeenCalledWith('ecommerce.refreshToken', 'refresh-2', expect.anything());
  });

  it('a 401 on refresh signs out, clears storage and runs onSignedOut', async () => {
    store.getItemAsync.mockResolvedValue('refresh-1');
    const signedOut = jest.fn();
    await startSession(tokens());
    setOnSignedOut(signedOut);
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => problemResponse(401, {}));

    expect(await refreshSession()).toBe(false);

    expect(getSessionSnapshot()).toEqual({ status: 'signed-out', user: null });
    expect(getAccessToken()).toBeUndefined();
    expect(store.deleteItemAsync).toHaveBeenCalled();
    expect(signedOut).toHaveBeenCalledTimes(1);
  });

  it('a network error on refresh keeps the session', async () => {
    store.getItemAsync.mockResolvedValue('refresh-1');
    await startSession(tokens());
    jest.spyOn(globalThis, 'fetch').mockRejectedValue(new TypeError('Network request failed'));

    expect(await refreshSession()).toBe(false);

    expect(getSessionSnapshot().status).toBe('signed-in');
    expect(getAccessToken()).toBe('access-1');
    expect(store.deleteItemAsync).not.toHaveBeenCalled();
  });

  it('restore on native with no stored token is signed out without a request', async () => {
    const spy = jest.spyOn(globalThis, 'fetch');

    await restoreSession();

    expect(getSessionSnapshot().status).toBe('signed-out');
    expect(spy).not.toHaveBeenCalled();
  });

  it('restore on web makes exactly one cookie refresh', async () => {
    platform.OS = 'web';
    const spy = jest
      .spyOn(globalThis, 'fetch')
      .mockImplementation(async () => jsonResponse(200, tokens({ refreshToken: null })));

    await restoreSession();

    expect(spy).toHaveBeenCalledTimes(1);
    expect((spy.mock.calls[0][1] as RequestInit).credentials).toBe('include');
    expect(getSessionSnapshot().status).toBe('signed-in');
  });

  it('restore with a network error ends up signed out but keeps the stored token', async () => {
    store.getItemAsync.mockResolvedValue('refresh-1');
    jest.spyOn(globalThis, 'fetch').mockRejectedValue(new TypeError('Network request failed'));

    await restoreSession();

    expect(getSessionSnapshot().status).toBe('signed-out');
    expect(store.deleteItemAsync).not.toHaveBeenCalled();
  });
});
