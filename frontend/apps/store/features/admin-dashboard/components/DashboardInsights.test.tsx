import { fireEvent, screen, waitFor } from 'expo-router/testing-library';
import { Text } from 'react-native';

import AdminDashboardScreen from '@/app/admin/index';
import { resetSession } from '@/features/auth/session';
import { saveTextFile } from '@/lib/platform';
import { makeDashboard, makeRecentOrdersPage, makeRevenueTrend } from '@/tests/admin-dashboard-fixtures';
import { calledUrls, jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { renderRouterWithQuery } from '@/tests/render-with-query';
import { signInAs } from '@/tests/session';

jest.mock('expo-secure-store');
jest.mock('@/lib/platform', () => ({ ...jest.requireActual('@/lib/platform'), saveTextFile: jest.fn(async () => undefined) }));

// P2-tier4: the revenue chart, the orders donut and the CSV export on the dashboard. Admin sees them; Support never requests or sees revenue.

const CSV = 'bucket_start_utc,revenue,orders,currency\r\n2026-11-08T12:00:00Z,200.00,2,USD\r\n';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
  jest.mocked(saveTextFile).mockClear();
});

afterEach(() => {
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const render = (url = '/admin') =>
  renderRouterWithQuery(
    {
      'admin/index': AdminDashboardScreen,
      'admin/orders/index': () => <Text>Orders list screen</Text>,
      'admin/analytics': () => <Text>Analytics screen</Text>,
    },
    { initialUrl: url },
  );

type Routes = { trend?: () => Response; exportCsv?: () => Response };

function mockApi(dashboard = makeDashboard(), routes: Routes = {}) {
  return jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const { pathname } = new URL(String(input));
    if (pathname === '/api/v1/admin/dashboard') return jsonResponse(200, dashboard);
    if (pathname === '/api/v1/admin/dashboard/recent-orders') return jsonResponse(200, makeRecentOrdersPage([]));
    if (pathname === '/api/v1/admin/dashboard/trend') return (routes.trend ?? (() => jsonResponse(200, makeRevenueTrend())))();
    if (pathname === '/api/v1/admin/dashboard/export') return (routes.exportCsv ?? (() => new Response(CSV, { status: 200 })))();
    return new Response('not mocked', { status: 599 });
  });
}

describe('Support gate (D9)', () => {
  it('S1: Support never requests the trend or the export and sees no chart, no Export CSV, no View report and no money', async () => {
    await signInAs('Support');
    const spy = mockApi(makeDashboard({ kpis: null, ordersByStatus: [{ status: 'Paid', count: 3 }] }));

    await render();

    expect(await screen.findByText('Orders by status')).toBeOnTheScreen();
    await waitFor(() => expect(calledUrls(spy).some((u) => u.includes('/recent-orders'))).toBe(true));
    expect(calledUrls(spy).filter((u) => u.includes('/trend') || u.includes('/export'))).toEqual([]);
    expect(screen.queryByTestId('revenue-trend-chart')).toBeNull();
    expect(screen.queryByText('Revenue trend')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Export CSV' })).toBeNull();
    expect(screen.queryByText('View report →')).toBeNull();
    expect(screen.queryByText(/\$/)).toBeNull();
  });
});

describe('Admin insights', () => {
  it('shows the server total and order count, the chart, and a View report link that keeps the period', async () => {
    await signInAs('Admin');
    mockApi(makeDashboard({ period: '7d' }), { trend: () => jsonResponse(200, makeRevenueTrend({ period: '7d' })) });

    await render('/admin?period=7d');

    expect(await screen.findByText('$2,345.60')).toBeOnTheScreen();
    expect(screen.getByText('9')).toBeOnTheScreen();
    expect(screen.getByTestId('revenue-trend-chart')).toBeOnTheScreen();
    await fireEvent.press(screen.getByText('View report →'));
    expect(await screen.findByText('Analytics screen')).toBeOnTheScreen();
    expect(screen).toHavePathname('/admin/analytics');
    expect(screen).toHaveSearchParams({ period: '7d' });
  });

  it('shows a Retry trend button when the trend fails and keeps the rest of the dashboard', async () => {
    await signInAs('Admin');
    const spy = mockApi(makeDashboard(), { trend: () => problemResponse(500, {}) });

    await render();

    expect(await screen.findByRole('button', { name: 'Retry trend' })).toBeOnTheScreen();
    expect(screen.getByText('Blue Shirt · 9 sold')).toBeOnTheScreen();
    const before = calledUrls(spy).filter((u) => u.includes('/trend')).length;
    await fireEvent.press(screen.getByRole('button', { name: 'Retry trend' }));
    await waitFor(() => expect(calledUrls(spy).filter((u) => u.includes('/trend')).length).toBeGreaterThan(before));
  });

  it('Refresh asks for the trend again as well as the dashboard', async () => {
    await signInAs('Admin');
    const spy = mockApi();
    await render();
    await screen.findByText('$2,345.60');
    await waitFor(() => expect(screen.getByRole('button', { name: 'Refresh' })).toBeEnabled());
    const count = (part: string) => calledUrls(spy).filter((u) => u.includes(part)).length;
    const trendBefore = count('/trend');
    const dashboardBefore = calledUrls(spy).filter((u) => /\/admin\/dashboard\?/.test(u)).length;

    await fireEvent.press(screen.getByRole('button', { name: 'Refresh' }));

    await waitFor(() => expect(count('/trend')).toBe(trendBefore + 1));
    expect(calledUrls(spy).filter((u) => /\/admin\/dashboard\?/.test(u)).length).toBe(dashboardBefore + 1);
  });

  it('Export CSV saves the server CSV text under the period file name', async () => {
    await signInAs('Admin');
    mockApi();
    await render();

    await fireEvent.press(await screen.findByRole('button', { name: 'Export CSV' }));

    await waitFor(() => expect(saveTextFile).toHaveBeenCalledWith('dashboard-today.csv', 'text/csv', CSV));
  });

  it('shows Export failed with the plain-language reason when the export request fails', async () => {
    await signInAs('Admin');
    mockApi(makeDashboard(), { exportCsv: () => problemResponse(403, {}) });
    await render();

    await fireEvent.press(await screen.findByRole('button', { name: 'Export CSV' }));

    expect(await screen.findByText("Export failed: You don't have access to that.")).toBeOnTheScreen();
    expect(saveTextFile).not.toHaveBeenCalled();
  });
});

describe('Orders by status donut', () => {
  const statuses = [
    { status: 'PendingPayment', count: 1 },
    { status: 'Paid', count: 2 },
    { status: 'Delivered', count: 1 },
  ];

  it('lists the four groups with counts and percents, and opens the filtered list from a single-status group or a member sub-link', async () => {
    await signInAs('Admin');
    mockApi(makeDashboard({ ordersByStatus: statuses }));
    await render();

    expect(await screen.findByTestId('orders-donut')).toBeOnTheScreen();
    for (const text of ['Awaiting payment', 'In progress', 'Completed', 'Cancelled or refunded', '1 (25%)', '2 (50%)', '0 (0%)']) {
      expect(screen.getAllByText(text).length).toBeGreaterThan(0);
    }
    await fireEvent.press(screen.getByText('Awaiting payment'));
    expect(await screen.findByText('Orders list screen')).toBeOnTheScreen();
    expect(screen).toHaveSearchParams({ status: 'PendingPayment' });
  });

  it('opens the list for a member status from a multi-status group sub-link', async () => {
    await signInAs('Admin');
    mockApi(makeDashboard({ ordersByStatus: statuses }));
    await render();

    await fireEvent.press(await screen.findByText('Paid: 2'));

    expect(await screen.findByText('Orders list screen')).toBeOnTheScreen();
    expect(screen).toHaveSearchParams({ status: 'Paid' });
  });

  it('shows No orders yet. and no donut when there are no orders', async () => {
    await signInAs('Admin');
    mockApi(makeDashboard({ ordersByStatus: [] }));
    await render();

    expect(await screen.findAllByText('No orders yet.')).not.toHaveLength(0);
    expect(screen.queryByTestId('orders-donut')).toBeNull();
  });
});
