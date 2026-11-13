import { fireEvent, screen } from 'expo-router/testing-library';

import CartScreen from '@/app/cart';
import OrderDetailScreen from '@/app/orders/[id]';
import { resetSession } from '@/features/auth/session';
import type { MergeResponse } from '@/features/cart/types';
import { makeCart } from '@/tests/cart-fixtures';
import { calledUrls, jsonResponse, mockFetchRoutes, problemResponse } from '@/tests/fetch-mock';
import { makeOrderDetail } from '@/tests/order-fixtures';
import { renderRouterWithQuery } from '@/tests/render-with-query';
import { signInAs } from '@/tests/session';

jest.mock('expo-secure-store');

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const mergeOk: MergeResponse = {
  cart: makeCart(),
  results: [{ variantId: 'v-1', outcome: 'Kept', quantity: 2, reason: null }],
};

const render = () => renderRouterWithQuery({ 'orders/[id]': OrderDetailScreen, cart: CartScreen }, { initialUrl: '/orders/o-1' });

async function renderLoaded(merge: () => Response, order = makeOrderDetail()) {
  await signInAs('Customer');
  const spy = mockFetchRoutes({
    '/api/v1/orders/o-1': () => jsonResponse(200, order),
    '/api/v1/cart/merge': merge,
    '/api/v1/cart': () => jsonResponse(200, makeCart()),
  });
  await render();
  await screen.findByRole('header', { name: 'Order ORD-2026-000001' });
  return spy;
}

const mergeCalls = (spy: jest.SpyInstance) => calledUrls(spy).filter((u) => u.endsWith('/api/v1/cart/merge'));
const reorderButton = (name = 'Reorder all items') => screen.getByRole('button', { name });

describe('OrderDetailView reorder', () => {
  it('S1 shows Reorder for a shipped order and keeps the header group', async () => {
    await renderLoaded(() => jsonResponse(200, mergeOk));

    expect(reorderButton()).toBeOnTheScreen();
    expect(screen.getByRole('header', { name: 'Order ORD-2026-000001' })).toBeOnTheScreen();
  });

  it('S2 press merges once, shows the result, and disables the button', async () => {
    const spy = await renderLoaded(() => jsonResponse(200, mergeOk));

    await fireEvent.press(reorderButton());

    const panel = await screen.findByRole('summary');
    expect(panel).toHaveTextContent('Added to your cart', { exact: false });
    expect(panel).toHaveTextContent('Black tee, M: 2 in your cart', { exact: false });
    expect(mergeCalls(spy)).toHaveLength(1);
    const done = screen.getByRole('button', { name: 'Items added to cart' });
    expect(done).toBeDisabled();
    await fireEvent.press(done);
    expect(mergeCalls(spy)).toHaveLength(1);
  });

  it('S3 shows every line of a mixed result', async () => {
    const order = makeOrderDetail();
    order.items = [...order.items, { ...order.items[0], variantId: 'v-2', name: 'Red cap' }];
    await renderLoaded(
      () =>
        jsonResponse(200, {
          cart: makeCart(),
          results: [
            { variantId: 'v-1', outcome: 'Capped', quantity: 1, reason: 'OutOfStock' },
            { variantId: 'v-2', outcome: 'Removed', quantity: 0, reason: 'Unavailable' },
          ],
        }),
      order,
    );

    await fireEvent.press(reorderButton());

    const panel = await screen.findByRole('summary');
    expect(panel).toHaveTextContent('Black tee, M: reduced to 1 (not enough in stock)', { exact: false });
    expect(panel).toHaveTextContent('Red cap: not added (no longer available)', { exact: false });
  });

  it('S4 a failed merge shows an alert and Try again resends the same key', async () => {
    let calls = 0;
    const spy = await renderLoaded(() => (++calls === 1 ? problemResponse(500, { title: 'boom' }) : jsonResponse(200, mergeOk)));

    await fireEvent.press(reorderButton());
    expect(await screen.findByRole('alert')).toBeOnTheScreen();

    await fireEvent.press(screen.getByRole('button', { name: 'Try again' }));
    await screen.findByRole('summary');

    const keys = spy.mock.calls
      .filter((c) => String(c[0]).endsWith('/api/v1/cart/merge'))
      .map((c) => (c[1]?.headers as Record<string, string>)['Idempotency-Key']);
    expect(keys).toHaveLength(2);
    expect(keys[0]).toBeTruthy();
    expect(keys[1]).toBe(keys[0]);
  });

  it('S5 is hidden for a PendingPayment order', async () => {
    await renderLoaded(() => jsonResponse(200, mergeOk), makeOrderDetail({ status: 'PendingPayment' }));

    expect(screen.queryByRole('button', { name: 'Reorder all items' })).toBeNull();
  });

  it('S6 View cart navigates to /cart', async () => {
    await renderLoaded(() => jsonResponse(200, mergeOk));
    await fireEvent.press(reorderButton());
    await screen.findByRole('summary');

    await fireEvent.press(screen.getByRole('link', { name: 'View cart' }));

    expect(screen).toHavePathname('/cart');
  });

  it('S7 while the request is pending the button says Adding items and is disabled', async () => {
    let release: (r: Response) => void = () => {};
    const held = new Promise<Response>((resolve) => {
      release = resolve;
    });
    await signInAs('Customer');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input);
      if (url.endsWith('/api/v1/cart/merge')) return held;
      return jsonResponse(200, makeOrderDetail());
    });
    await render();
    await screen.findByRole('header', { name: 'Order ORD-2026-000001' });

    await fireEvent.press(reorderButton());

    expect(await screen.findByRole('button', { name: 'Adding items to cart' })).toBeDisabled();
    release(jsonResponse(200, mergeOk));
    await screen.findByRole('summary');
  });
});
