import { jsonResponse } from '@/tests/fetch-mock';

import { fetchOrder, fetchOrders } from './api';

const BASE = 'http://api.test.invalid:5080';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = BASE;
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

function spyFetch() {
  return jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, {}));
}

describe('orders api', () => {
  it('fetchOrders sends the page and the page size', async () => {
    const spy = spyFetch();

    await fetchOrders(3);

    expect(String(spy.mock.calls[0][0])).toBe(`${BASE}/api/v1/orders?page=3&pageSize=10`);
  });

  it('fetchOrder URI-encodes the id', async () => {
    const spy = spyFetch();

    await fetchOrder('a/b c');

    expect(String(spy.mock.calls[0][0])).toBe(`${BASE}/api/v1/orders/a%2Fb%20c`);
  });
});
