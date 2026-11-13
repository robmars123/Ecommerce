import { screen } from 'expo-router/testing-library';

import OrderDetailScreen from '@/app/orders/[id]';
import { resetSession } from '@/features/auth/session';
import type { OrderDetail } from '@/features/orders/types';
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
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

async function renderOrder(overrides: Partial<OrderDetail> = {}) {
  await signInAs('Customer');
  jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeOrderDetail(overrides)));
  await renderRouterWithQuery({ 'orders/[id]': OrderDetailScreen }, { initialUrl: '/orders/o-1' });
  await screen.findByRole('header', { name: 'Order ORD-2026-000001' });
}

describe('OrderDetailView timeline descriptions', () => {
  it('J1 shows the description of every timeline entry', async () => {
    await renderOrder();

    expect(screen.getByText('We received your order and held your items for payment.')).toBeOnTheScreen();
    expect(screen.getByText('Your payment was confirmed.')).toBeOnTheScreen();
    expect(screen.getByText('We started preparing your order.')).toBeOnTheScreen();
    expect(screen.getByText('Your order was handed to the carrier.')).toBeOnTheScreen();
  });

  it('J2 shows the expiry description together with the reason tag', async () => {
    const description = 'Payment was not completed before the hold on your items ran out, so we released them and cancelled the order.';
    await renderOrder({
      status: 'Cancelled',
      timeline: [
        { fromStatus: null, toStatus: 'PendingPayment', at: '2026-03-04T10:00:00Z', reason: null, description: 'Held.' },
        { fromStatus: 'PendingPayment', toStatus: 'Cancelled', at: '2026-03-04T11:00:00Z', reason: 'reservation-expired', description },
      ],
    });

    expect(screen.getByText(description)).toBeOnTheScreen();
    expect(screen.getByText('reservation-expired')).toBeOnTheScreen();
  });

  it('J3 renders the server text verbatim', async () => {
    await renderOrder({
      timeline: [
        { fromStatus: null, toStatus: 'Paid', at: '2026-03-04T10:00:00Z', reason: null, description: 'Text only the server knows.' },
      ],
    });

    expect(screen.getByText('Text only the server knows.')).toBeOnTheScreen();
  });
});
