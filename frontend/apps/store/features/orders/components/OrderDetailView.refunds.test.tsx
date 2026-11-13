import { screen } from 'expo-router/testing-library';

import OrderDetailScreen from '@/app/orders/[id]';
import { resetSession } from '@/features/auth/session';
import { subscribeToOrderStatus } from '@/lib/realtime';
import { jsonResponse } from '@/tests/fetch-mock';
import { makeOrderDetail } from '@/tests/order-fixtures';
import { renderRouterWithQuery } from '@/tests/render-with-query';
import { signInAs } from '@/tests/session';

jest.mock('expo-secure-store');

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  resetSession();
  jest.mocked(subscribeToOrderStatus).mockClear();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const render = () => renderRouterWithQuery({ 'orders/[id]': OrderDetailScreen }, { initialUrl: '/orders/o-1' });

// F12 carry-in (Slice 6): refund rows render, and a live status message makes the screen refetch.
describe('OrderDetailView refunds and live status', () => {
  it('lists each refund with its amount, plain-language status and date, in the order the server sends them', async () => {
    await signInAs('Customer');
    const order = makeOrderDetail({
      status: 'Paid',
      refunds: [
        { amount: { amount: '10.00', currency: 'USD' }, status: 'Succeeded', createdAt: '2026-03-07T10:00:00Z' },
        { amount: { amount: '5.00', currency: 'USD' }, status: 'Pending', createdAt: '2026-03-08T10:00:00Z' },
        { amount: { amount: '2.50', currency: 'USD' }, status: 'Failed', createdAt: '2026-03-09T10:00:00Z' },
      ],
    });
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, order));

    await render();

    expect(await screen.findByText('Refunds')).toBeOnTheScreen();
    const rows = screen.getAllByText(/ · (Refunded|Being processed|Could not be refunded)$/).map((node) => node.props.children as string);
    expect(rows).toEqual(['$10.00 · Refunded', '$5.00 · Being processed', '$2.50 · Could not be refunded']);
    expect(screen.getByText('Mar 7, 2026')).toBeOnTheScreen();
  });

  it('shows no Refunds section when there are none', async () => {
    await signInAs('Customer');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeOrderDetail()));

    await render();
    await screen.findByRole('header', { name: 'Order ORD-2026-000001' });

    expect(screen.queryByText('Refunds')).toBeNull();
  });

  it('shows the return form for a Delivered order only', async () => {
    await signInAs('Customer');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeOrderDetail({ status: 'Delivered' })));

    await render();

    expect(await screen.findByText('Return items')).toBeOnTheScreen();
  });

  it('a live status message for this order refetches it and shows the new status from the server', async () => {
    await signInAs('Customer');
    let current = makeOrderDetail({ status: 'Shipped' });
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, current));
    await render();
    await screen.findByRole('header', { name: 'Order ORD-2026-000001' });
    const listener = jest.mocked(subscribeToOrderStatus).mock.calls.at(-1)![0];
    const callsBefore = spy.mock.calls.length;

    current = makeOrderDetail({ status: 'Delivered' });
    listener('o-1');

    expect(await screen.findByText('Return items')).toBeOnTheScreen();
    expect(spy.mock.calls.length).toBeGreaterThan(callsBefore);
  });

  it('a live message about another order does not refetch', async () => {
    await signInAs('Customer');
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeOrderDetail()));
    await render();
    await screen.findByRole('header', { name: 'Order ORD-2026-000001' });
    const listener = jest.mocked(subscribeToOrderStatus).mock.calls.at(-1)![0];
    const callsBefore = spy.mock.calls.length;

    listener('o-99');

    expect(spy.mock.calls.length).toBe(callsBefore);
  });
});
