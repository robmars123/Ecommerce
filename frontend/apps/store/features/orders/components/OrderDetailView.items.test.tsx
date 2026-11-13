import { screen } from 'expo-router/testing-library';

import OrderDetailScreen from '@/app/orders/[id]';
import { resetSession } from '@/features/auth/session';
import type { OrderDetail, OrderDetailItem } from '@/features/orders/types';
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

const base = makeOrderDetail().items[0];

describe('OrderDetailView item thumbnail and colour', () => {
  it('shows the thumbnail and colour', async () => {
    const item: OrderDetailItem = {
      ...base,
      image: { url: 'https://img.test.invalid/tee.jpg', altText: 'Black tee, front' },
      colour: 'Black',
    };
    await renderOrder({ items: [item] });

    expect(screen.getByLabelText('Black tee, front')).toBeOnTheScreen();
    expect(screen.getByText('Colour: Black')).toBeOnTheScreen();
    expect(screen.getByLabelText('Black tee, M, Black × 2, $40.00')).toBeOnTheScreen();
  });

  it('shows a placeholder and no colour line when both are null', async () => {
    await renderOrder();

    expect(screen.getByRole('image', { name: 'Black tee, M' })).toBeOnTheScreen();
    expect(screen.queryByText(/Colour:/)).toBeNull();
    expect(screen.getByLabelText('Black tee, M × 2, $40.00')).toBeOnTheScreen();
  });

  it('shows each row its own colour', async () => {
    const red: OrderDetailItem = { ...base, variantId: 'v-2', sku: 'TEE-RED-M', name: 'Red tee, M', colour: 'Red' };
    await renderOrder({ items: [base, red] });

    expect(screen.getAllByText(/Colour:/)).toHaveLength(1);
    expect(screen.getByText('Colour: Red')).toBeOnTheScreen();
  });
});
