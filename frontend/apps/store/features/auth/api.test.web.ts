/**
 * Runs in the web Jest project only. Moved from api.test.ts, where it faked Platform.OS = 'web'; the platform is now chosen
 * by file (lib/platform.web.tsx), so the web transport is proven here. Node environment: jsdom has no fetch or Response.
 * @jest-environment node
 */
import { jsonResponse } from '@/tests/fetch-mock';
import { makeTokens } from '@/tests/session';

import { refresh } from './api';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const headers = (spy: jest.SpyInstance) => (spy.mock.calls[0][1] as RequestInit).headers as Record<string, string>;

describe('auth api (web)', () => {
  it('web: asks for the cookie transport and sends an empty body', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeTokens('Customer')));

    await refresh(null);

    expect(headers(spy)['X-Auth-Transport']).toBe('cookie');
    expect((spy.mock.calls[0][1] as RequestInit).credentials).toBe('include');
    expect(JSON.parse((spy.mock.calls[0][1] as RequestInit).body as string)).toEqual({});
  });
});
