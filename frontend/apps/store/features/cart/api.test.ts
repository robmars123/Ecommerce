import { ApiError, setAuthHandlers } from '@/lib/api-client';
import { jsonResponse, problemResponse } from '@/tests/fetch-mock';

import { addCartItem, mergeCart, removeCartItem, setCartItem } from './api';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test';
  setAuthHandlers({ getAccessToken: () => 'tok', refresh: async () => false });
});
afterEach(() => {
  jest.restoreAllMocks();
  setAuthHandlers(undefined);
});

function spyFetch(response: Response) {
  return jest.spyOn(globalThis, 'fetch').mockImplementation(async () => response);
}

describe('cart api', () => {
  it('add sends the variant and quantity only, never a price', async () => {
    const spy = spyFetch(jsonResponse(200, { items: [], subtotal: { amount: '0.00', currency: 'USD' } }));

    await addCartItem('v-1', 2);

    const [url, init] = spy.mock.calls[0];
    expect(String(url)).toBe('http://api.test/api/v1/cart/items');
    expect(init?.method).toBe('POST');
    expect(JSON.parse(String(init?.body))).toEqual({ variantId: 'v-1', quantity: 2 });
  });

  it('set uses PUT on the variant with the quantity only, and remove uses DELETE', async () => {
    const spy = spyFetch(jsonResponse(200, { items: [] }));

    await setCartItem('v-1', 3);
    expect(spy.mock.calls[0][1]?.method).toBe('PUT');
    expect(JSON.parse(String(spy.mock.calls[0][1]?.body))).toEqual({ quantity: 3 });

    spy.mockImplementation(async () => new Response(null, { status: 204 }));
    await removeCartItem('v-1');
    expect(spy.mock.calls[1][1]?.method).toBe('DELETE');
    expect(String(spy.mock.calls[1][0])).toBe('http://api.test/api/v1/cart/items/v-1');
  });

  it('merge sends the Idempotency-Key header and lines without any price', async () => {
    const spy = spyFetch(jsonResponse(200, { cart: { items: [] }, results: [] }));

    await mergeCart([{ variantId: 'v-1', quantity: 2 }], 'key-1');

    const init = spy.mock.calls[0][1];
    expect((init?.headers as Record<string, string>)['Idempotency-Key']).toBe('key-1');
    expect(JSON.parse(String(init?.body))).toEqual({ lines: [{ variantId: 'v-1', quantity: 2 }] });
  });

  it('a 409 stock reply carries the available quantity on the error', async () => {
    spyFetch(problemResponse(409, { title: 'Not enough stock', available: 3 }));

    const error = await addCartItem('v-1', 9).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).kind).toBe('conflict');
    expect((error as ApiError).available).toBe(3);
  });
});
