import { calledUrls, jsonResponse, mockFetchRoutes } from '@/tests/fetch-mock';
import { makeAdminOrderDetail, makeReturnPage, usd } from '@/tests/admin-order-fixtures';
import { queryKeys } from '@/lib/query-keys';

import { decideReturn, fetchAdminOrder, fetchReturns, submitRefund } from './api';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

describe('admin orders api', () => {
  it('fetches an order by id, encoding it', async () => {
    const spy = mockFetchRoutes({ '/api/v1/admin/orders/a%2Fb': () => jsonResponse(200, makeAdminOrderDetail()) });

    await fetchAdminOrder('a/b');

    expect(calledUrls(spy)).toEqual(['http://api.test.invalid:5080/api/v1/admin/orders/a%2Fb']);
  });

  it('posts the refund with the key in the Idempotency-Key header and the amount as a string', async () => {
    const spy = mockFetchRoutes({ '/refund': () => jsonResponse(202, { refundId: 'rf-1' }) });

    await submitRefund('o-9', { amount: usd('5.00'), reason: 'why' }, 'key-1');

    const [url, init] = spy.mock.calls[0] as [string, RequestInit];
    expect(url).toBe('http://api.test.invalid:5080/api/v1/admin/orders/o-9/refund');
    expect(init.method).toBe('POST');
    expect((init.headers as Record<string, string>)['Idempotency-Key']).toBe('key-1');
    expect(JSON.parse(init.body as string)).toEqual({ amount: { amount: '5.00', currency: 'USD' }, reason: 'why' });
  });

  it('lists returns with the status and page, and no status for all', async () => {
    const spy = mockFetchRoutes({ '/api/v1/admin/returns': () => jsonResponse(200, makeReturnPage([])) });

    await fetchReturns('Approved', 2);
    await fetchReturns('all', 1);

    expect(calledUrls(spy)).toEqual([
      'http://api.test.invalid:5080/api/v1/admin/returns?status=Approved&page=2&pageSize=20',
      'http://api.test.invalid:5080/api/v1/admin/returns?page=1&pageSize=20',
    ]);
  });

  it('posts a decision with its reason', async () => {
    const spy = mockFetchRoutes({ '/approve': () => new Response(null, { status: 204 }) });

    await decideReturn('r-1', 'approve', 'fine');

    const [url, init] = spy.mock.calls[0] as [string, RequestInit];
    expect(url).toBe('http://api.test.invalid:5080/api/v1/admin/returns/r-1/approve');
    expect(JSON.parse(init.body as string)).toEqual({ reason: 'fine' });
  });
});

describe('query keys for admin orders and returns', () => {
  it('are distinct per order, status and page and share the returns prefix', () => {
    expect(queryKeys.admin.orders.detail('a')).not.toEqual(queryKeys.admin.orders.detail('b'));
    expect(queryKeys.admin.returns.list('Requested', 1)).not.toEqual(queryKeys.admin.returns.list('Requested', 2));
    expect(queryKeys.admin.returns.list('all', 1)).not.toEqual(queryKeys.admin.returns.list('Requested', 1));
    expect(queryKeys.admin.returns.list('all', 1).slice(0, 2)).toEqual([...queryKeys.admin.returns.all]);
  });
});
