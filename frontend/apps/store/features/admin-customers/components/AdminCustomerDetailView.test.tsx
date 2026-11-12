import { fireEvent, screen } from 'expo-router/testing-library';
import { Text } from 'react-native';

import AdminCustomerDetailScreen from '@/app/admin/customers/[id]';
import { resetSession } from '@/features/auth/session';
import { makeCustomerDetail } from '@/tests/admin-customer-fixtures';
import { usd } from '@/tests/admin-order-fixtures';
import { jsonResponse, mockFetchRoutes, problemResponse } from '@/tests/fetch-mock';
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

const render = () =>
  renderRouterWithQuery(
    { 'admin/customers/[id]': AdminCustomerDetailScreen, 'admin/orders/[id]': () => <Text>Order detail screen</Text> },
    { initialUrl: '/admin/customers/c-1' },
  );
const serve = (body: unknown) => mockFetchRoutes({ '/api/v1/admin/customers/c-1': () => jsonResponse(200, body) });

describe('AdminCustomerDetailView', () => {
  it('C1 shows the total the server sent and never a sum of the recent orders', async () => {
    await signInAs('Admin');
    serve(makeCustomerDetail({ totalSpent: usd('110.00') }));

    await render();

    expect(await screen.findByText('Total spent $110.00')).toBeOnTheScreen();
    expect(screen.queryByText(/\$140\.00/)).toBeNull();
    expect(screen.getByText('Ann Lee')).toBeOnTheScreen();
    expect(screen.getByText('ann@example.test')).toBeOnTheScreen();
    expect(screen.getByText(/Customer since Jan 5, 2026/)).toBeOnTheScreen();
    expect(screen.getByText('Orders 2')).toBeOnTheScreen();
  });

  it('C2 shows no total line when the server sends null, and the rest still renders', async () => {
    await signInAs('Admin');
    serve(makeCustomerDetail({ totalSpent: null }));

    await render();

    expect(await screen.findByText('Ann Lee')).toBeOnTheScreen();
    expect(screen.queryByText(/Total spent/)).toBeNull();
    expect(screen.getByText('ann@example.test')).toBeOnTheScreen();
    expect(screen.getByText(/Customer since/)).toBeOnTheScreen();
    expect(screen.getByText('Orders 2')).toBeOnTheScreen();
    expect(screen.getByRole('header', { name: 'Recent orders' })).toBeOnTheScreen();
    expect(screen.getByRole('header', { name: 'Recent returns' })).toBeOnTheScreen();
    expect(screen.getByRole('header', { name: 'Recent refunds' })).toBeOnTheScreen();
    expect(screen.getByText(/ORD-2026-000001 · .* · Paid · \$100\.00/)).toBeOnTheScreen();
    expect(screen.getByText(/ORD-2026-000002 · .* · Shipped · \$40\.00/)).toBeOnTheScreen();
  });

  it('C3 says when the lists are truncated and links an order to its screen', async () => {
    await signInAs('Admin');
    const many = Array.from({ length: 10 }, (_, i) => ({
      id: `o-${i}`,
      orderNumber: `ORD-2026-0001${i}0`,
      createdAt: '2026-03-10T10:00:00Z',
      status: 'Paid' as const,
      grandTotal: usd('1.00'),
    }));
    serve(makeCustomerDetail({ orderCount: 12, recentOrders: many }));

    await render();

    expect(await screen.findByText('Showing 10 of 12')).toBeOnTheScreen();
    await fireEvent.press(screen.getByText(/ORD-2026-000100/));
    expect(await screen.findByText('Order detail screen')).toBeOnTheScreen();
  });

  it('C3 has no Showing line when the counts equal the list lengths', async () => {
    await signInAs('Admin');
    serve(makeCustomerDetail({ orderCount: 2, returnCount: 1, refundCount: 2 }));

    await render();

    await screen.findByText('Ann Lee');
    expect(screen.queryByText(/Showing/)).toBeNull();
  });

  it('C4 shows returns and refunds with their status and the Automatic marker only when set', async () => {
    await signInAs('Admin');
    serve(makeCustomerDetail());

    await render();

    expect(await screen.findByText(/ORD-2026-000001 · Requested · Too small/)).toBeOnTheScreen();
    expect(screen.getByText(/ORD-2026-000001 · \$20\.00 · Refunded · Automatic/)).toBeOnTheScreen();
    expect(screen.getByText(/ORD-2026-000002 · \$5\.00 · Being processed$/)).toBeOnTheScreen();
    expect(screen.getAllByText(/Automatic/)).toHaveLength(1);
  });

  it('C5 shows the empty messages and a zero total', async () => {
    await signInAs('Admin');
    serve(
      makeCustomerDetail({
        orderCount: 0,
        recentOrders: [],
        returnCount: 0,
        recentReturns: [],
        refundCount: 0,
        recentRefunds: [],
        totalSpent: usd('0.00'),
      }),
    );

    await render();

    expect(await screen.findByText('No orders yet.')).toBeOnTheScreen();
    expect(screen.getByText('No returns.')).toBeOnTheScreen();
    expect(screen.getByText('No refunds.')).toBeOnTheScreen();
    expect(screen.getByText('Total spent $0.00')).toBeOnTheScreen();
  });

  it('C6 shows a not-found message without retry on 404', async () => {
    await signInAs('Admin');
    mockFetchRoutes({ '/api/v1/admin/customers/c-1': () => problemResponse(404, { title: 'Not found' }) });

    await render();

    expect(await screen.findByText('We could not find that customer.')).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Try again' })).toBeNull();
  });

  it('C6 shows a plain error on 500 and Try again refetches', async () => {
    await signInAs('Admin');
    let fail = true;
    const spy = mockFetchRoutes({
      '/api/v1/admin/customers/c-1': () => (fail ? problemResponse(500, { title: 'boom' }) : jsonResponse(200, makeCustomerDetail())),
    });
    await render();
    const retry = await screen.findByRole('button', { name: 'Try again' });
    expect(screen.queryByText('Ann Lee')).toBeNull();

    fail = false;
    await fireEvent.press(retry);

    expect(await screen.findByText('Ann Lee')).toBeOnTheScreen();
    expect(spy.mock.calls.length).toBe(2);
  });
});
