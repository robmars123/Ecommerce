import { act, renderHook, waitFor } from '@testing-library/react-native';

import { jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { createTestQueryClient, queryWrapper } from '@/tests/render-with-query';

import type { CheckoutBody } from '../types';
import { useAttemptKey } from './useAttemptKey';
import { usePlaceOrderMutation } from './usePlaceOrderMutation';
import { useRetryPaymentMutation } from './useRetryPaymentMutation';

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

const reply = { order: { id: 'o-1' }, clientSecret: 'cs_1' };

const keysSent = (spy: jest.SpyInstance) =>
  spy.mock.calls.map((call) => ((call[1] as RequestInit).headers as Record<string, string>)['Idempotency-Key']);

describe('useAttemptKey (F4)', () => {
  it('makes the key on first use, keeps it, and makes a new one only after renew()', async () => {
    const { result } = await renderHook(() => useAttemptKey());

    const first = result.current.current();
    expect(result.current.current()).toBe(first);

    result.current.renew();

    expect(result.current.current()).not.toBe(first);
  });
});

describe('usePlaceOrderMutation (F4, F5)', () => {
  const render = () => renderHook(() => usePlaceOrderMutation(), { wrapper: queryWrapper(createTestQueryClient()) });

  it('a double submit while the request is in flight sends one request', async () => {
    let release: (r: Response) => void = () => undefined;
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(() => new Promise<Response>((resolve) => (release = resolve)));
    const { result } = await render();

    await act(async () => {
      result.current.submit(body);
      result.current.submit(body);
    });
    await waitFor(() => expect(result.current.isPending).toBe(true));
    expect(spy).toHaveBeenCalledTimes(1);

    await act(async () => release(jsonResponse(200, reply)));
    await waitFor(() => expect(result.current.isPending).toBe(false));
  });

  it('a network retry reuses the key; after the order is placed the next attempt gets a new key', async () => {
    const spy = jest
      .spyOn(globalThis, 'fetch')
      .mockRejectedValueOnce(new TypeError('Network request failed'))
      .mockImplementation(async () => jsonResponse(200, reply));
    const { result } = await render();

    await act(async () => result.current.submit(body));
    await waitFor(() => expect(result.current.error).not.toBeNull());
    await act(async () => result.current.submit(body));
    await waitFor(() => expect(result.current.isPending).toBe(false));
    await act(async () => result.current.submit(body));
    await waitFor(() => expect(spy).toHaveBeenCalledTimes(3));

    const [failed, retried, next] = keysSent(spy);
    expect(retried).toBe(failed);
    expect(next).not.toBe(failed);
  });

  it('a 503 provider outage repeats with the same key; a definitive 409 gets a new key', async () => {
    const spy = jest
      .spyOn(globalThis, 'fetch')
      .mockImplementationOnce(async () => problemResponse(503, { type: 'payment-provider-unavailable', orderId: 'o-1' }))
      .mockImplementationOnce(async () => problemResponse(409, { type: 'cart-empty' }))
      .mockImplementation(async () => jsonResponse(200, reply));
    const { result } = await render();

    for (let i = 0; i < 3; i += 1) {
      await act(async () => result.current.submit(body));
      await waitFor(() => expect(result.current.isPending).toBe(false));
    }

    const [unavailable, conflict, fixed] = keysSent(spy);
    expect(conflict).toBe(unavailable);
    expect(fixed).not.toBe(conflict);
  });
});

describe('useRetryPaymentMutation (F11)', () => {
  const render = () => renderHook(() => useRetryPaymentMutation('o-1'), { wrapper: queryWrapper(createTestQueryClient()) });

  it('one key per retry: reused after a network failure, new after a success; the secret goes to the callback', async () => {
    const spy = jest
      .spyOn(globalThis, 'fetch')
      .mockRejectedValueOnce(new TypeError('Network request failed'))
      .mockImplementation(async () => jsonResponse(200, { orderId: 'o-1', attempt: 2, clientSecret: 'cs_2' }));
    const onSecret = jest.fn();
    const { result } = await render();

    await act(async () => result.current.retry(onSecret));
    await waitFor(() => expect(result.current.error).not.toBeNull());
    await act(async () => result.current.retry(onSecret));
    await waitFor(() => expect(onSecret).toHaveBeenCalledWith('cs_2'));
    await act(async () => result.current.retry(onSecret));
    await waitFor(() => expect(spy).toHaveBeenCalledTimes(3));

    const [failed, retried, next] = keysSent(spy);
    expect(retried).toBe(failed);
    expect(next).not.toBe(failed);
    expect(String(spy.mock.calls[0][0])).toBe('http://api.test.invalid:5080/api/v1/orders/o-1/retry-payment');
  });
});
