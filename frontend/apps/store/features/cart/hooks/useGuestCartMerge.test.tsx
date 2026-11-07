import { act, cleanup, renderHook, waitFor } from '@testing-library/react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';

import { resetSession } from '@/features/auth/session';
import { queryKeys } from '@/lib/query-keys';
import { jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { createTestQueryClient, queryWrapper } from '@/tests/render-with-query';
import { signInAs } from '@/tests/session';

import { useGuestCartStore } from '../guest-cart-store';
import { useMergeResultStore } from '../merge-result-store';
import type { MergeResponse } from '../types';
import { useGuestCartMerge } from './useGuestCartMerge';

// F9: the merge runner. Lines are cleared only after a 200; a retry reuses one key; a 409 drops it.
const money = (amount: string) => ({ amount, currency: 'USD' });
const mergedCart: MergeResponse = {
  cart: { items: [], subtotal: money('0.00') },
  results: [{ variantId: 'v-1', outcome: 'Kept', quantity: 2, reason: null }],
};

function addLine() {
  useGuestCartStore.getState().add(
    {
      variantId: 'v-1',
      productSlug: 'blue-shirt',
      snapshot: { productName: 'Blue Shirt', variantName: 'Medium', price: money('24.50'), image: null },
    },
    2,
  );
}

function keysSent(spy: jest.SpyInstance): string[] {
  return spy.mock.calls.map((c) => (c[1]?.headers as Record<string, string>)['Idempotency-Key']);
}

async function render() {
  const client = createTestQueryClient();
  const view = await renderHook(() => useGuestCartMerge(), { wrapper: queryWrapper(client) });
  return { client, ...view };
}

beforeEach(async () => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test';
  // Let writes from the previous test settle, then start from empty storage and a hydrated store.
  await new Promise((resolve) => setTimeout(resolve, 0));
  await AsyncStorage.clear();
  await useGuestCartStore.persist.rehydrate();
  useGuestCartStore.setState({ lines: [], mergeKey: null });
  useMergeResultStore.getState().dismiss();
});
afterEach(async () => {
  await cleanup();
  jest.restoreAllMocks();
  resetSession();
});

describe('useGuestCartMerge', () => {
  it('sends nothing while signed out, or while signed in with an empty device cart', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, mergedCart));
    addLine();
    await render();
    await act(async () => {});
    expect(spy).not.toHaveBeenCalled();

    useGuestCartStore.setState({ lines: [] });
    await signInAs('Customer');
    await act(async () => {});
    expect(spy).not.toHaveBeenCalled();
  });

  it('signed in with lines: merges once, clears the lines and key on 200, publishes the results and the cart', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, mergedCart));
    await signInAs('Customer');
    addLine();
    const { client } = await render();

    await waitFor(() => expect(useGuestCartStore.getState().lines).toEqual([]));

    expect(spy).toHaveBeenCalledTimes(1);
    expect(JSON.parse(String(spy.mock.calls[0][1]?.body))).toEqual({ lines: [{ variantId: 'v-1', quantity: 2 }] });
    expect(useGuestCartStore.getState().mergeKey).toBeNull();
    expect(useMergeResultStore.getState().results).toEqual([{ ...mergedCart.results[0], name: 'Blue Shirt - Medium' }]);
    expect(client.getQueryData(queryKeys.cart)).toEqual(mergedCart.cart);
  });

  it('a failed merge keeps the lines and the key and marks the failure', async () => {
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => problemResponse(500, { title: 'boom' }));
    await signInAs('Customer');
    addLine();
    await render();

    await waitFor(() => expect(useMergeResultStore.getState().failed).toBe(true));

    expect(useGuestCartStore.getState().lines).toHaveLength(1);
    expect(useGuestCartStore.getState().mergeKey).not.toBeNull();
  });

  it('retry sends the same Idempotency-Key as the failed attempt', async () => {
    const spy = jest
      .spyOn(globalThis, 'fetch')
      .mockImplementationOnce(async () => problemResponse(500, { title: 'boom' }))
      .mockImplementation(async () => jsonResponse(200, mergedCart));
    await signInAs('Customer');
    addLine();
    await render();
    await waitFor(() => expect(useMergeResultStore.getState().failed).toBe(true));

    await act(async () => {
      useMergeResultStore.getState().retry?.();
    });
    await waitFor(() => expect(useGuestCartStore.getState().lines).toEqual([]));

    const keys = keysSent(spy);
    expect(keys).toHaveLength(2);
    expect(keys[0]).toBeTruthy();
    expect(keys[1]).toBe(keys[0]);
  });

  it('a 409 drops the key, so the retry gets a new one', async () => {
    const spy = jest
      .spyOn(globalThis, 'fetch')
      .mockImplementationOnce(async () => problemResponse(409, { title: 'Conflict' }))
      .mockImplementation(async () => jsonResponse(200, mergedCart));
    await signInAs('Customer');
    addLine();
    await render();
    await waitFor(() => expect(useMergeResultStore.getState().failed).toBe(true));
    expect(useGuestCartStore.getState().mergeKey).toBeNull();

    await act(async () => {
      useMergeResultStore.getState().retry?.();
    });
    await waitFor(() => expect(useGuestCartStore.getState().lines).toEqual([]));

    const keys = keysSent(spy);
    expect(keys[1]).toBeTruthy();
    expect(keys[1]).not.toBe(keys[0]);
  });

  it('keeps one request in flight however often it re-renders', async () => {
    let release: (response: Response) => void = () => {};
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(
      () => new Promise<Response>((resolve) => { release = resolve; }),
    );
    await signInAs('Customer');
    addLine();
    const { rerender } = await render();
    await waitFor(() => expect(spy).toHaveBeenCalledTimes(1));

    await rerender({});
    await rerender({});
    await act(async () => {});
    expect(spy).toHaveBeenCalledTimes(1);

    await act(async () => release(jsonResponse(200, mergedCart)));
    await waitFor(() => expect(useGuestCartStore.getState().lines).toEqual([]));
  });
});
