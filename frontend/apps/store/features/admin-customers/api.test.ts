import { ApiError } from '@/lib/api-client';
import { makeCustomerDetail, makeCustomerPage } from '@/tests/admin-customer-fixtures';
import { calledUrls, jsonResponse, mockFetchRoutes, problemResponse } from '@/tests/fetch-mock';

import { fetchAdminCustomer, fetchAdminCustomers } from './api';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

describe('admin customers api', () => {
  it('A1 lists with search, page and page size, and no search when empty', async () => {
    const spy = mockFetchRoutes({ '/api/v1/admin/customers': () => jsonResponse(200, makeCustomerPage([])) });

    await fetchAdminCustomers('ann', 2);
    await fetchAdminCustomers(undefined, 1);
    await fetchAdminCustomers('', 1);

    const urls = calledUrls(spy).map((u) => new URL(u));
    expect(urls[0].pathname).toBe('/api/v1/admin/customers');
    expect(Object.fromEntries(urls[0].searchParams)).toEqual({ search: 'ann', page: '2', pageSize: '20' });
    expect(urls[1].searchParams.has('search')).toBe(false);
    expect(urls[2].searchParams.has('search')).toBe(false);
    expect(Object.fromEntries(urls[1].searchParams)).toEqual({ page: '1', pageSize: '20' });
  });

  it('A2 fetches a customer by encoded id and rejects a 404 with ApiError', async () => {
    const spy = mockFetchRoutes({ '/api/v1/admin/customers/a%2Fb': () => jsonResponse(200, makeCustomerDetail()) });
    await fetchAdminCustomer('a/b');
    expect(calledUrls(spy)).toEqual(['http://api.test.invalid:5080/api/v1/admin/customers/a%2Fb']);

    mockFetchRoutes({ '/api/v1/admin/customers/': () => problemResponse(404, { title: 'Not found' }) });
    const error: unknown = await fetchAdminCustomer('x').catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(404);
  });
});
