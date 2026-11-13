import { screen } from 'expo-router/testing-library';

import OrderDetailScreen from '@/app/orders/[id]';
import { resetSession } from '@/features/auth/session';
import type { OrderDetail, OrderStatus } from '@/features/orders/types';
import { jsonResponse } from '@/tests/fetch-mock';
import { makeOrderDetail } from '@/tests/order-fixtures';
import { renderRouterWithQuery } from '@/tests/render-with-query';
import { signInAs } from '@/tests/session';

jest.mock('expo-secure-store');

const NOTE =
  'No payment is kept for this order. It was cancelled before payment was confirmed, and any payment that went through late is refunded automatically.';

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

describe('OrderDetailView totals note', () => {
  it('J1 shows the note under the Total for a Cancelled order', async () => {
    const order = makeOrderDetail({ status: 'Cancelled' });
    await renderOrder({ status: 'Cancelled' });

    expect(screen.getByText(NOTE)).toBeOnTheScreen();
    expect(screen.getByLabelText(`Total $${order.grandTotal.amount}`)).toBeOnTheScreen();
  });

  it.each<OrderStatus>(['PendingPayment', 'Paid', 'Processing', 'Shipped', 'Delivered', 'Refunded', 'Returned'])(
    'J2 %s has no totals note',
    async (status) => {
      await renderOrder({ status });

      expect(screen.queryByText(/No payment is kept/)).toBeNull();
    },
  );

  it('J3 is independent of statusNote and payment', async () => {
    await renderOrder({ status: 'Cancelled', statusNote: null, payment: null });
    expect(screen.getByText(NOTE)).toBeOnTheScreen();
  });

  it('J3 shows alongside the payment card', async () => {
    await renderOrder({
      status: 'Cancelled',
      payment: { method: 'card', amountDue: { amount: '0.00', currency: 'USD' }, captured: { amount: '0.00', currency: 'USD' } },
    });
    expect(screen.getByText(NOTE)).toBeOnTheScreen();
    expect(screen.getByLabelText('Captured $0.00')).toBeOnTheScreen();
  });
});
