import { fireEvent, screen, waitFor } from 'expo-router/testing-library';
import { Text } from 'react-native';

import AdminOrderListScreen from '@/app/admin/orders/index';
import { resetSession } from '@/features/auth/session';
import { makeAdminListItem, makeAdminListPage } from '@/tests/admin-order-fixtures';
import { calledUrls, jsonResponse, mockFetchRoutes, problemResponse } from '@/tests/fetch-mock';
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

const render = (url = '/admin/orders') =>
  renderRouterWithQuery(
    { 'admin/orders/index': AdminOrderListScreen, 'admin/orders/[id]': () => <Text>Order detail screen</Text> },
    { initialUrl: url },
  );

const params = (spy: jest.SpyInstance, call: number) => Object.fromEntries(new URL(calledUrls(spy)[call]).searchParams);
const rows = [makeAdminListItem(), makeAdminListItem({ id: 'o-2', orderNumber: 'ORD-2026-000002', customerEmail: null, status: 'Shipped' })];

describe('AdminOrderListView', () => {
  it('renders each order with its customer, status and total, linking to the detail screen', async () => {
    await signInAs('Admin');
    mockFetchRoutes({ '/api/v1/admin/orders': () => jsonResponse(200, makeAdminListPage(rows)) });

    await render();

    expect(await screen.findByText(/ORD-2026-000001 · a@example\.test · .* · Paid · \$25\.00/)).toBeOnTheScreen();
    expect(screen.getByText(/ORD-2026-000002 · Unknown customer · .* · Shipped/)).toBeOnTheScreen();
    await fireEvent.press(screen.getByText(/ORD-2026-000001/));
    expect(await screen.findByText('Order detail screen')).toBeOnTheScreen();
  });

  it('starts filtered by the status in the route', async () => {
    await signInAs('Admin');
    const spy = mockFetchRoutes({ '/api/v1/admin/orders': () => jsonResponse(200, makeAdminListPage(rows)) });

    await render('/admin/orders?status=Paid');

    await screen.findByText(/ORD-2026-000001/);
    expect(params(spy, 0).status).toBe('Paid');
  });

  it('ignores an unknown status in the route', async () => {
    await signInAs('Admin');
    const spy = mockFetchRoutes({ '/api/v1/admin/orders': () => jsonResponse(200, makeAdminListPage(rows)) });

    await render('/admin/orders?status=Bogus');

    await screen.findByText(/ORD-2026-000001/);
    expect(params(spy, 0).status).toBeUndefined();
  });

  it('applies the filters and goes back to page 1', async () => {
    await signInAs('Admin');
    const spy = mockFetchRoutes({ '/api/v1/admin/orders': () => jsonResponse(200, makeAdminListPage(rows, { totalPages: 3, totalCount: 50 })) });
    await render();
    await screen.findByText(/ORD-2026-000001/);
    await fireEvent.press(screen.getByRole('button', { name: 'Next page' }));
    await waitFor(() => expect(spy.mock.calls.some((c) => String(c[0]).includes('page=2'))).toBe(true));

    await fireEvent.changeText(screen.getByLabelText('Customer email'), 'a@example.test');
    await fireEvent.changeText(screen.getByLabelText('From (yyyy-mm-dd)'), '2026-03-01');
    await fireEvent.press(screen.getByRole('button', { name: 'Apply filters' }));

    await waitFor(() => expect(params(spy, spy.mock.calls.length - 1)).toMatchObject({ customer: 'a@example.test', from: '2026-03-01', page: '1' }));
  });

  it('shows a field error for an invalid date and sends nothing', async () => {
    await signInAs('Admin');
    const spy = mockFetchRoutes({ '/api/v1/admin/orders': () => jsonResponse(200, makeAdminListPage(rows)) });
    await render();
    await screen.findByText(/ORD-2026-000001/);
    const before = spy.mock.calls.length;

    await fireEvent.changeText(screen.getByLabelText('To (yyyy-mm-dd)'), '2026-02-30');
    await fireEvent.press(screen.getByRole('button', { name: 'Apply filters' }));

    expect(screen.getByText('Use a date like 2026-03-31.')).toBeOnTheScreen();
    expect(spy.mock.calls.length).toBe(before);
  });

  it('shows the empty state', async () => {
    await signInAs('Admin');
    mockFetchRoutes({ '/api/v1/admin/orders': () => jsonResponse(200, makeAdminListPage([])) });

    await render();

    expect(await screen.findByText('No orders match.')).toBeOnTheScreen();
  });

  it('shows an error with a retry that refetches', async () => {
    await signInAs('Admin');
    let fail = true;
    mockFetchRoutes({
      '/api/v1/admin/orders': () => (fail ? problemResponse(500, { title: 'boom' }) : jsonResponse(200, makeAdminListPage(rows))),
    });
    await render();
    const retry = await screen.findByRole('button', { name: 'Try again' });

    fail = false;
    await fireEvent.press(retry);

    expect(await screen.findByText(/ORD-2026-000001/)).toBeOnTheScreen();
  });

  it('pages with the pager', async () => {
    await signInAs('Admin');
    mockFetchRoutes({ '/api/v1/admin/orders': () => jsonResponse(200, makeAdminListPage(rows, { totalPages: 2, totalCount: 30 })) });

    await render();

    expect(await screen.findByText('Page 1 of 2')).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Previous page' })).toBeDisabled();
  });
});
