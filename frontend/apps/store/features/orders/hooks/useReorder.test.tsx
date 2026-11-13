import { act, cleanup, renderHook, waitFor } from '@testing-library/react-native';

import { resetSession } from '@/features/auth/session';
import { queryKeys } from '@/lib/query-keys';
import { calledUrls, jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { makeCart } from '@/tests/cart-fixtures';
import { makeOrderDetail } from '@/tests/order-fixtures';
import { createTestQueryClient, queryWrapper } from '@/tests/render-with-query';
import { signInAs } from '@/tests/session';

import type { MergeResponse } from '@/features/cart/types';
import type { OrderItem } from '../types';
import { reorderLines, useReorder } from './useReorder';

const money = (amount: string) => ({ amount, currency: 'USD' });
const mergeOk: MergeResponse = {
  cart: makeCart(),
  results: [{ variantId: 'v-1', outcome: 'Kept', quantity: 2, reason: null }],
};

function item(variantId: string, quantity: number, name = `Item ${variantId}`): OrderItem {
  return { variantId, sku: `SKU-${variantId}`, name, unitPrice: money('20.00'), quantity, lineTotal: money('40.00') };
}
const items: OrderItem[] = [...makeOrderDetail().items, item('v-2', 1)];

function keysSent(spy: jest.SpyInstance): string[] {
  return spy.mock.calls.map((c) => (c[1]?.headers as Record<string, string>)['Idempotency-Key']);
}
function bodiesSent(spy: jest.SpyInstance): unknown[] {
  return spy.mock.calls.map((c) => JSON.parse(String(c[1]?.body)));
}

async function render(orderItems: OrderItem[] = items) {
  const client = createTestQueryClient();
  const view = await renderHook(() => useReorder(orderItems), { wrapper: queryWrapper(client) });
  return { client, ...view };
}

beforeEach(async () => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test';
  await signInAs('Customer');
});
afterEach(async () => {
  await cleanup();
  jest.restoreAllMocks();
  resetSession();
});

describe('useReorder', () => {
  it('H1 sends only variantId and quantity, never a price, to POST /cart/merge with an Idempotency-Key', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, mergeOk));
    const { result } = await render();

    await act(async () => {
      result.current.reorder();
    });
    await waitFor(() => expect(result.current.state.status).toBe('done'));

    expect(spy).toHaveBeenCalledTimes(1);
    expect(calledUrls(spy)[0]).toMatch(/\/api\/v1\/cart\/merge$/);
    expect(spy.mock.calls[0][1]?.method).toBe('POST');
    const body = bodiesSent(spy)[0] as { lines: Record<string, unknown>[] };
    expect(body).toEqual({
      lines: [
        { variantId: 'v-1', quantity: 2 },
        { variantId: 'v-2', quantity: 1 },
      ],
    });
    for (const line of body.lines) {
      for (const forbidden of ['unitPrice', 'lineTotal', 'price', 'sku', 'name']) {
        expect(line).not.toHaveProperty(forbidden);
      }
    }
    expect(keysSent(spy)[0]).toBeTruthy();
  });

  it('H2 two synchronous calls send one request', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, mergeOk));
    const { result } = await render();

    await act(async () => {
      result.current.reorder();
      result.current.reorder();
    });
    await waitFor(() => expect(result.current.state.status).toBe('done'));

    expect(spy).toHaveBeenCalledTimes(1);
  });

  it('H3 a retry after a network failure reuses the key and body; after a 409 it uses a new key', async () => {
    const spy = jest
      .spyOn(globalThis, 'fetch')
      .mockImplementationOnce(async () => {
        throw new TypeError('Network request failed');
      })
      .mockImplementationOnce(async () => problemResponse(409, { title: 'Conflict' }))
      .mockImplementation(async () => jsonResponse(200, mergeOk));
    const { result } = await render();

    await act(async () => {
      result.current.reorder();
    });
    await waitFor(() => expect(result.current.state.status).toBe('failed'));
    await act(async () => {
      result.current.reorder();
    });
    await waitFor(() => expect(spy).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(result.current.state.status).toBe('failed'));

    const keys = keysSent(spy);
    expect(keys[0]).toBeTruthy();
    expect(keys[1]).toBe(keys[0]);
    expect(bodiesSent(spy)[1]).toEqual(bodiesSent(spy)[0]);

    await act(async () => {
      result.current.reorder();
    });
    await waitFor(() => expect(result.current.state.status).toBe('done'));
    expect(keysSent(spy)[2]).toBeTruthy();
    expect(keysSent(spy)[2]).not.toBe(keys[0]);
  });

  it('H4 reorderLines aggregates duplicate variants in first-occurrence order', () => {
    expect(reorderLines([item('v-1', 2), item('v-2', 5), item('v-1', 1)])).toEqual([
      { variantId: 'v-1', quantity: 3 },
      { variantId: 'v-2', quantity: 5 },
    ]);
  });

  it('H5 success writes the cart to the query cache and maps names by variantId', async () => {
    const response: MergeResponse = {
      cart: makeCart(),
      results: [
        { variantId: 'v-1', outcome: 'Kept', quantity: 2, reason: null },
        { variantId: 'v-x', outcome: 'Removed', quantity: 0, reason: 'Unavailable' },
      ],
    };
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, response));
    const { result, client } = await render();

    await act(async () => {
      result.current.reorder();
    });
    await waitFor(() => expect(result.current.state.status).toBe('done'));

    expect(client.getQueryData(queryKeys.cart)).toEqual(response.cart);
    expect(result.current.state).toEqual({
      status: 'done',
      lines: [
        { ...response.results[0], name: 'Black tee, M' },
        { ...response.results[1], name: 'Item' },
      ],
    });
  });

  it('H6 after done, reorder sends nothing more', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, mergeOk));
    const { result } = await render();

    await act(async () => {
      result.current.reorder();
    });
    await waitFor(() => expect(result.current.state.status).toBe('done'));
    await act(async () => {
      result.current.reorder();
    });

    expect(spy).toHaveBeenCalledTimes(1);
  });
});
