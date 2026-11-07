import { jsonResponse } from '@/tests/fetch-mock';
import { makeTokens } from '@/tests/session';

import { confirmPasswordReset, login, logout, refresh, register, requestPasswordReset } from './api';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const headers = (spy: jest.SpyInstance) => (spy.mock.calls[0][1] as RequestInit).headers as Record<string, string>;

describe('auth api', () => {
  it('sends no bearer token on any auth call', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeTokens('Customer')));

    await login({ email: 'a@example.test', password: 'p' });
    await register({ email: 'a@example.test', password: 'p', displayName: 'A' });

    for (const call of spy.mock.calls) {
      expect(((call[1] as RequestInit).headers as Record<string, string>).Authorization).toBeUndefined();
    }
  });

  it('native: native tokens travel in the body and no cookie header is sent', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeTokens('Customer')));

    await refresh('stored-refresh');

    expect(JSON.parse((spy.mock.calls[0][1] as RequestInit).body as string)).toEqual({ refreshToken: 'stored-refresh' });
    expect(headers(spy)['X-Auth-Transport']).toBeUndefined();
  });

  it('logout, reset request and reset confirm hit their endpoints', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => new Response(null, { status: 204 }));

    await logout('t');
    await requestPasswordReset('a@example.test');
    await confirmPasswordReset({ token: 't', newPassword: 'n' });

    expect(spy.mock.calls.map((c) => new URL(String(c[0])).pathname)).toEqual([
      '/api/v1/auth/logout',
      '/api/v1/auth/password-reset',
      '/api/v1/auth/password-reset/confirm',
    ]);
  });
});
