import { jsonResponse } from '@/tests/fetch-mock';

import { fetchShippingMethods, placeOrder, retryPayment } from './api';
import type { CheckoutBody } from './types';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const body: CheckoutBody = {
  shippingAddress: { fullName: 'A B', line1: '1 Main St', city: 'Town', postalCode: '12345', country: 'US' },
  shippingMethodCode: 'standard',
};

describe('checkout api', () => {
  it('fetchShippingMethods calls the shipping-methods endpoint', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, { items: [] }));

    await fetchShippingMethods();

    expect(String(spy.mock.calls[0][0])).toBe('http://api.test.invalid:5080/api/v1/shipping-methods');
  });

  it('placeOrder posts the body as given with the idempotency key header', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, {}));

    await placeOrder(body, 'key-1');

    const [url, init] = spy.mock.calls[0];
    expect(String(url)).toBe('http://api.test.invalid:5080/api/v1/checkout');
    expect(init?.method).toBe('POST');
    expect((init?.headers as Record<string, string>)['Idempotency-Key']).toBe('key-1');
    expect(JSON.parse(String(init?.body))).toEqual(body);
  });

  it('retryPayment posts to the order with the idempotency key and URI-encodes the id', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, {}));

    await retryPayment('o/1', 'key-2');

    const [url, init] = spy.mock.calls[0];
    expect(String(url)).toBe('http://api.test.invalid:5080/api/v1/orders/o%2F1/retry-payment');
    expect((init?.headers as Record<string, string>)['Idempotency-Key']).toBe('key-2');
  });
});
