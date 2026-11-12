import { ApiError } from '@/lib/api-client';
import { makeSearchResult } from '@/tests/admin-search-fixtures';
import { calledUrls, jsonResponse, mockFetchRoutes, problemResponse } from '@/tests/fetch-mock';

import { fetchAdminSearch } from './api';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

describe('admin search api', () => {
  it('A1 sends q encoded, passes the signal and returns the body', async () => {
    const body = makeSearchResult();
    const spy = mockFetchRoutes({ '/api/v1/admin/search': () => jsonResponse(200, body) });
    const controller = new AbortController();

    const result = await fetchAdminSearch('ab & c', controller.signal);

    expect(result).toEqual(body);
    expect(spy).toHaveBeenCalledTimes(1);
    const url = new URL(calledUrls(spy)[0]);
    expect(url.pathname).toBe('/api/v1/admin/search');
    expect(Object.fromEntries(url.searchParams)).toEqual({ q: 'ab & c' });
    expect((spy.mock.calls[0][1] as RequestInit).signal).toBeTruthy();
  });

  it('A2 rejects a 404 with ApiError', async () => {
    mockFetchRoutes({ '/api/v1/admin/search': () => problemResponse(404, { title: 'Not found' }) });
    const error: unknown = await fetchAdminSearch('ab').catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(404);
  });
});
