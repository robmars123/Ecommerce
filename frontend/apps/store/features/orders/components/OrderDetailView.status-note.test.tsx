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

const TITLE = 'This order was cancelled';
const MESSAGE = 'We held your items while you paid, but the hold ran out. Nothing will ship.';
const cancelled: Partial<OrderDetail> = {
  status: 'Cancelled',
  statusNote: { pillNote: 'Payment not completed in time', alert: { title: TITLE, message: MESSAGE } },
};

async function renderOrder(overrides: Partial<OrderDetail> = {}) {
  await signInAs('Customer');
  jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeOrderDetail(overrides)));
  await renderRouterWithQuery({ 'orders/[id]': OrderDetailScreen }, { initialUrl: '/orders/o-1' });
  await screen.findByRole('header', { name: 'Order ORD-2026-000001' });
}

describe('OrderDetailView status note', () => {
  it('shows the pill note and the cancellation alert for a cancelled order', async () => {
    await renderOrder(cancelled);

    expect(screen.getByText('Payment not completed in time')).toBeOnTheScreen();
    expect(screen.getByLabelText(`${TITLE}. ${MESSAGE}`)).toBeOnTheScreen();
    expect(screen.getByText(TITLE)).toBeOnTheScreen();
    expect(screen.getByText(MESSAGE)).toBeOnTheScreen();
  });

  it('shows only the pill note when the alert is null', async () => {
    await renderOrder({ status: 'Refunded', statusNote: { pillNote: 'Refunded in full to your original payment method', alert: null } });

    expect(screen.getByText('Refunded in full to your original payment method')).toBeOnTheScreen();
    expect(screen.queryByText(TITLE)).toBeNull();
  });

  it('shows neither when there is no status note', async () => {
    await renderOrder();

    expect(screen.queryByText('Payment not completed in time')).toBeNull();
    expect(screen.queryByText(TITLE)).toBeNull();
  });

  it('never invents cancellation copy when the server sends no note', async () => {
    await renderOrder({ status: 'Cancelled', statusNote: null });

    expect(screen.queryByText(TITLE)).toBeNull();
    expect(screen.queryByLabelText(/This order was cancelled/)).toBeNull();
  });

  it('keeps the items and timeline next to the alert', async () => {
    await renderOrder(cancelled);

    expect(screen.getByText('Black tee, M')).toBeOnTheScreen();
    expect(screen.getByText('Timeline')).toBeOnTheScreen();
  });
});
