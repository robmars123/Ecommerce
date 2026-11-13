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

describe('OrderDetailView payment card', () => {
  it('shows method, amount due and captured', async () => {
    await renderOrder({
      status: 'PendingPayment',
      payment: { method: 'card', amountDue: { amount: '40.00', currency: 'USD' }, captured: { amount: '0.00', currency: 'USD' } },
    });

    expect(screen.getByText('Payment')).toBeOnTheScreen();
    expect(screen.getByText('Card')).toBeOnTheScreen();
    expect(screen.getByLabelText('Amount due $40.00')).toBeOnTheScreen();
    expect(screen.getByLabelText('Captured $0.00')).toBeOnTheScreen();
  });

  it("shows the server's amounts verbatim", async () => {
    await renderOrder({
      status: 'Paid',
      grandTotal: { amount: '40.00', currency: 'USD' },
      payment: { method: 'card', amountDue: { amount: '0.00', currency: 'USD' }, captured: { amount: '12.34', currency: 'USD' } },
    });

    expect(screen.getByLabelText('Captured $12.34')).toBeOnTheScreen();
    expect(screen.getByLabelText('Amount due $0.00')).toBeOnTheScreen();
  });

  it('hides the card when payment is null', async () => {
    await renderOrder();

    expect(screen.queryByText('Card')).toBeNull();
    expect(screen.queryByLabelText(/Amount due/)).toBeNull();
    expect(screen.queryByLabelText(/Captured/)).toBeNull();
    expect(screen.queryByText('Payment')).toBeNull();
  });
});
