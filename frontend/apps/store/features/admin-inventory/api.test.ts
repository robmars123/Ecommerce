import { jsonResponse } from '@/tests/fetch-mock';
import { makeInventoryItem, makeInventoryPage } from '@/tests/fixtures';

import { adjustStock, fetchAdminInventory } from './api';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

describe('admin inventory api', () => {
  it('lists with page and pageSize', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeInventoryPage([])));

    await fetchAdminInventory(2, 20);

    expect(spy.mock.calls[0][0]).toBe('http://api.test.invalid:5080/api/v1/admin/inventory?page=2&pageSize=20');
  });

  it('posts the exact adjust body', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeInventoryItem()));
    const body = { variantId: 'v-1', delta: -3, expectedOnHand: 10, reason: 'Damaged' };

    await adjustStock(body);

    const [url, init] = spy.mock.calls[0] as [string, RequestInit];
    expect(url).toBe('http://api.test.invalid:5080/api/v1/admin/inventory/adjust');
    expect(init.method).toBe('POST');
    expect(JSON.parse(init.body as string)).toEqual(body);
  });
});
