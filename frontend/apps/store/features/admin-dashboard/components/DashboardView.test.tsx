import { fireEvent, screen, waitFor, within } from 'expo-router/testing-library';
import { Text } from 'react-native';

import AdminDashboardScreen from '@/app/admin/index';
import { DashboardView } from '@/features/admin-dashboard/components/DashboardView';
import { resetSession } from '@/features/auth/session';
import { makeDashboard, makeRecentOrder, makeRecentOrdersPage } from '@/tests/admin-dashboard-fixtures';
import { calledUrls, jsonResponse, problemResponse } from '@/tests/fetch-mock';
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
    {
      'admin/index': AdminDashboardScreen,
      'admin/orders/index': () => <Text>Orders list screen</Text>,
      'admin/returns': () => <Text>Returns screen</Text>,
      'admin/inventory': () => <Text>Inventory screen</Text>,
    },
    { initialUrl: '/admin' },
  );

function mockApi(dashboard = makeDashboard(), recent = makeRecentOrdersPage([makeRecentOrder()])) {
  return jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const { pathname } = new URL(String(input));
    if (pathname === '/api/v1/admin/dashboard') return jsonResponse(200, dashboard);
    if (pathname === '/api/v1/admin/dashboard/recent-orders') return jsonResponse(200, recent);
    return new Response('not mocked', { status: 599 });
  });
}

describe('DashboardView', () => {
  it('shows the figures exactly as the server returned them, with the as-of time', async () => {
    await signInAs('Admin');
    mockApi();

    await render();

    expect(await screen.findByLabelText('Revenue')).toBeOnTheScreen();
    const revenue = within(screen.getByLabelText('Revenue'));
    expect(revenue.getByText('$1,234.50')).toBeOnTheScreen();
    expect(revenue.getByText('Paid minus refunds today')).toBeOnTheScreen();
    expect(revenue.getByText('+23.5%')).toBeOnTheScreen();
    expect(revenue.getByText('vs $1,000.00 previous period')).toBeOnTheScreen();
    const orders = within(screen.getByLabelText('Orders'));
    expect(orders.getByText('7')).toBeOnTheScreen();
    expect(orders.getByText('Paid today')).toBeOnTheScreen();
    expect(orders.getByText('0 delivered · 0 cancelled')).toBeOnTheScreen();
    const average = within(screen.getByLabelText('Average order value'));
    expect(average.getByText('$176.36')).toBeOnTheScreen();
    expect(average.getByText('Across 7 orders')).toBeOnTheScreen();
    const customers = within(screen.getByLabelText('Customers'));
    expect(customers.getByText('5')).toBeOnTheScreen();
    expect(customers.getByText('3 new · 2 returning')).toBeOnTheScreen();
    expect(screen.getByText('Figures as of Nov 8, 2026, 14:30 UTC')).toBeOnTheScreen();
    expect(screen.getByText('Blue Shirt · 9 sold')).toBeOnTheScreen();
    expect(screen.getByText('Blue Shirt (BS-M) · 2 left')).toBeOnTheScreen();
  });

  it('hides the sales figures when the server sent none (Support)', async () => {
    await signInAs('Support');
    mockApi(makeDashboard({ kpis: null }));

    await render();

    expect(await screen.findByText('Orders by status')).toBeOnTheScreen();
    expect(screen.queryByText('Sales')).toBeNull();
    expect(screen.queryByText(/Revenue/)).toBeNull();
    expect(screen.queryByText(/previous period/)).toBeNull();
    expect(screen.queryByText(/%/)).toBeNull();
  });

  it('opens the orders list filtered by the tapped status', async () => {
    await signInAs('Admin');
    mockApi();
    await render();

    await fireEvent.press(await screen.findByText('Paid: 4'));

    expect(await screen.findByText('Orders list screen')).toBeOnTheScreen();
    expect(screen).toHavePathname('/admin/orders');
    expect(screen).toHaveSearchParams({ status: 'Paid' });
  });

  it('links a low-stock row to inventory', async () => {
    await signInAs('Admin');
    mockApi();
    await render();

    await fireEvent.press(await screen.findByText('Blue Shirt (BS-M) · 2 left'));

    expect(await screen.findByText('Inventory screen')).toBeOnTheScreen();
    expect(screen).toHavePathname('/admin/inventory');
  });

  it('raises dead letters and a lagging projector as alerts and links pending returns', async () => {
    await signInAs('Admin');
    mockApi(
      makeDashboard({
        needsAttention: {
          pendingReturns: 2,
          deadLetters: [{ consumer: 'notifications.order-email', count: 3 }],
          laggingProjectors: [{ name: 'admin.dashboard', lagSeconds: 125 }],
        },
      }),
    );

    await render();

    expect(await screen.findByText('notifications.order-email: 3 failed events need attention')).toBeOnTheScreen();
    expect(screen.getByText('admin.dashboard is 125 seconds behind')).toBeOnTheScreen();
    expect(screen.getAllByRole('alert')).toHaveLength(2);
    expect(screen.queryByText('Nothing needs attention.')).toBeNull();
    await fireEvent.press(screen.getByText('2 return requests waiting for a decision'));
    expect(await screen.findByText('Returns screen')).toBeOnTheScreen();
  });

  it('says nothing needs attention when there is nothing', async () => {
    await signInAs('Admin');
    mockApi();

    await render();

    expect(await screen.findByText('Nothing needs attention.')).toBeOnTheScreen();
  });

  it('asks the server again when the period changes and when Refresh is pressed', async () => {
    await signInAs('Admin');
    const spy = mockApi();
    await render();
    await screen.findByText('$1,234.50');

    await fireEvent.press(screen.getByRole('button', { name: '30 days' }));
    await waitFor(() => expect(calledUrls(spy).some((u) => u.includes('period=30d'))).toBe(true));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Refresh' })).toBeEnabled());
    await fireEvent.press(screen.getByRole('button', { name: 'Refresh' }));

    const dashboardCalls = calledUrls(spy).filter((u) => u.includes('/admin/dashboard?'));
    expect(dashboardCalls[0]).toContain('period=today');
    expect(dashboardCalls.some((u) => u.includes('period=30d'))).toBe(true);
    await waitFor(() => expect(calledUrls(spy).filter((u) => u.includes('/admin/dashboard?')).length).toBeGreaterThanOrEqual(3));
    expect(screen.getByRole('button', { name: '30 days' })).toBeSelected();
  });

  it('shows the error with a retry that loads the dashboard', async () => {
    await signInAs('Admin');
    let fail = true;
    jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const { pathname } = new URL(String(input));
      if (pathname === '/api/v1/admin/dashboard/recent-orders') return jsonResponse(200, makeRecentOrdersPage([]));
      return fail ? problemResponse(500, { title: 'Server error' }) : jsonResponse(200, makeDashboard());
    });
    await render();

    const retry = (await screen.findAllByRole('button', { name: 'Try again' }))[0];
    fail = false;
    await fireEvent.press(retry);

    expect(await screen.findByText('$1,234.50')).toBeOnTheScreen();
  });

  it('lists recent orders and pages through them', async () => {
    await signInAs('Admin');
    const spy = mockApi(makeDashboard(), makeRecentOrdersPage([makeRecentOrder({ status: 'PendingPayment' })], { totalCount: 25 }));
    await render();

    expect(await screen.findByText('ORD-2026-000001 · a@example.test · Nov 8, 2026 · $45.00 · PendingPayment')).toBeOnTheScreen();
    expect(screen.getByText('Page 1 of 3')).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: 'Next orders' }));

    await screen.findByText('Page 2 of 3');
    expect(calledUrls(spy).some((u) => u.endsWith('/recent-orders?page=2&pageSize=10'))).toBe(true);
  });

  it('offers the quick actions', async () => {
    await signInAs('Admin');
    mockApi();

    await render();

    for (const label of ['Add product', 'Categories', 'Inventory', 'Orders', 'Returns']) {
      expect(await screen.findByRole('link', { name: label })).toBeOnTheScreen();
    }
  });

  it.each(['Admin', 'Support'] as const)('D1 shows no revenue when the server sent no KPIs (%s)', async (role) => {
    await signInAs(role);
    mockApi(makeDashboard({ kpis: null }));

    await render();

    expect(await screen.findByText('Orders by status')).toBeOnTheScreen();
    for (const label of ['Revenue', 'Orders', 'Average order value', 'Customers']) {
      expect(screen.queryByLabelText(label)).toBeNull();
    }
    expect(screen.queryByText('Paid minus refunds today')).toBeNull();
  });

  it('D2 never leaves a card blank when the figures are zero', async () => {
    await signInAs('Admin');
    const zero = { amount: '0.00', currency: 'USD' };
    mockApi(
      makeDashboard({
        period: 'today',
        kpis: {
          revenue: zero,
          orderCount: 0,
          averageOrderValue: zero,
          customers: { total: 0, new: 0, returning: 0 },
          previousRevenue: zero,
          revenueChangePercent: null,
        },
      }),
    );

    await render();

    const revenue = within(await screen.findByLabelText('Revenue'));
    expect(revenue.getByText('$0.00')).toBeOnTheScreen();
    expect(revenue.getByText('Paid minus refunds today')).toBeOnTheScreen();
    const orders = within(screen.getByLabelText('Orders'));
    expect(orders.getByText('0')).toBeOnTheScreen();
    expect(orders.getByText('No paid orders today')).toBeOnTheScreen();
    const customers = within(screen.getByLabelText('Customers'));
    expect(customers.getByText('0')).toBeOnTheScreen();
    expect(customers.getByText('No customers today')).toBeOnTheScreen();
  });

  it('D3 words the period from the response, not the selected chip', async () => {
    await signInAs('Admin');
    mockApi(makeDashboard({ period: '30d' }));

    await render();

    expect(await screen.findByText('Paid minus refunds in the last 30 days')).toBeOnTheScreen();
    expect(screen.getByText('Paid in the last 30 days')).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Today' })).toBeSelected();
  });

  it('Q1 shows a Support user only the Orders quick action', async () => {
    await signInAs('Support');
    mockApi(makeDashboard({ kpis: null }));

    await render();

    expect(await screen.findByRole('link', { name: 'Orders' })).toBeOnTheScreen();
    for (const label of ['Add product', 'Categories', 'Inventory', 'Returns']) {
      expect(screen.queryByRole('link', { name: label })).toBeNull();
    }
  });

  it.each([
    ['Admin', ['Add product', 'Categories', 'Inventory', 'Orders', 'Returns']],
    ['Support', ['Orders']],
  ] as const)('Q2 offers %s exactly these quick actions', async (role, expected) => {
    await signInAs(role);
    mockApi(makeDashboard({ kpis: role === 'Support' ? null : makeDashboard().kpis }));

    await render();

    await screen.findByText('Quick actions');
    const visible = ['Add product', 'Categories', 'Inventory', 'Orders', 'Returns'].filter(
      (label) => screen.queryByRole('link', { name: label }) !== null,
    );
    expect(visible).toEqual(expected);
  });

  it('Q3 opens the orders list from the Support Orders action', async () => {
    await signInAs('Support');
    mockApi(makeDashboard({ kpis: null }));
    await render();

    await fireEvent.press(await screen.findByRole('link', { name: 'Orders' }));

    expect(await screen.findByText('Orders list screen')).toBeOnTheScreen();
    expect(screen).toHavePathname('/admin/orders');
  });

  it('B1 shows the low-stock banner to an Admin and opens inventory from it', async () => {
    await signInAs('Admin');
    mockApi();
    await render();

    const banner = await screen.findByRole('link', { name: 'Stock is running low. Review inventory' });
    expect(banner).toBeOnTheScreen();
    expect(screen.getByText('Stock is running low')).toBeOnTheScreen();
    expect(screen.getByText('Review inventory')).toBeOnTheScreen();
    await fireEvent.press(banner);

    expect(await screen.findByText('Inventory screen')).toBeOnTheScreen();
    expect(screen).toHavePathname('/admin/inventory');
  });

  it('B2 shows no banner when nothing is running low', async () => {
    await signInAs('Admin');
    mockApi(makeDashboard({ lowStock: [] }));
    await render();

    await screen.findByText('Nothing is running low.');
    expect(screen.queryByRole('link', { name: 'Stock is running low. Review inventory' })).toBeNull();
    expect(screen.queryByText('Stock is running low')).toBeNull();
  });

  it('B3 hides the banner from Support even when stock is low', async () => {
    await signInAs('Support');
    mockApi(makeDashboard({ kpis: null }));
    await render();

    await screen.findByText('Orders by status');
    expect(screen.queryByText('Stock is running low')).toBeNull();
  });

  it('B4 shows no banner before the dashboard has loaded', async () => {
    await signInAs('Admin');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const { pathname } = new URL(String(input));
      if (pathname === '/api/v1/admin/dashboard/recent-orders') return jsonResponse(200, makeRecentOrdersPage([]));
      return problemResponse(500, { title: 'Server error' });
    });
    await render();

    await screen.findAllByRole('button', { name: 'Try again' });
    expect(screen.queryByText('Stock is running low')).toBeNull();
  });
});

describe('DashboardView period, refresh and as-of (P2.14-P2.16)', () => {
  const renderAt = (initialUrl: string) =>
    renderRouterWithQuery(
      {
        'admin/index': AdminDashboardScreen,
        'admin/orders/index': () => <Text>Orders list screen</Text>,
        'admin/returns': () => <Text>Returns screen</Text>,
        'admin/inventory': () => <Text>Inventory screen</Text>,
      },
      { initialUrl },
    );

  const dashboardCalls = (spy: jest.SpyInstance) => calledUrls(spy).filter((u) => u.includes('/admin/dashboard?'));

  it('U1 reads the period from the URL', async () => {
    await signInAs('Admin');
    const spy = mockApi();

    await renderAt('/admin?period=7d');

    await screen.findByText('$1,234.50');
    expect(dashboardCalls(spy)[0]).toContain('period=7d');
    expect(screen.getByRole('button', { name: '7 days' })).toBeSelected();
  });

  it('U2 writes the period to the URL and drops it for Today', async () => {
    await signInAs('Admin');
    const spy = mockApi();
    await renderAt('/admin');
    await screen.findByText('$1,234.50');

    await fireEvent.press(screen.getByRole('button', { name: '30 days' }));
    await waitFor(() => expect(screen).toHaveSearchParams({ period: '30d' }));
    await waitFor(() => expect(dashboardCalls(spy).some((u) => u.includes('period=30d'))).toBe(true));

    await fireEvent.press(screen.getByRole('button', { name: 'Today' }));
    await waitFor(() => expect(screen).toHaveSearchParams({}));
  });

  it('U3 falls back to Today for an unknown period in the URL', async () => {
    await signInAs('Admin');
    const spy = mockApi();

    await renderAt('/admin?period=1y');

    await screen.findByText('$1,234.50');
    expect(calledUrls(spy).some((u) => u.includes('period=1y'))).toBe(false);
    expect(dashboardCalls(spy)[0]).toContain('period=today');
    expect(screen.getByRole('button', { name: 'Today' })).toBeSelected();
  });

  it('R1 keeps the old figures and says the refresh failed', async () => {
    await signInAs('Admin');
    let fail = false;
    jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const { pathname } = new URL(String(input));
      if (pathname === '/api/v1/admin/dashboard') {
        return fail ? problemResponse(500, { title: 'Server error' }) : jsonResponse(200, makeDashboard());
      }
      return jsonResponse(200, makeRecentOrdersPage([]));
    });
    await renderAt('/admin');
    await screen.findByText('$1,234.50');
    await waitFor(() => expect(screen.getByRole('button', { name: 'Refresh' })).toBeEnabled());

    fail = true;
    await fireEvent.press(screen.getByRole('button', { name: 'Refresh' }));

    expect(await screen.findByText(/^Refresh failed: .* Showing the last figures\.$/)).toBeOnTheScreen();
    expect(screen.getByText('$1,234.50')).toBeOnTheScreen();
    expect(screen.getByText('Figures as of Nov 8, 2026, 14:30 UTC')).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Try again' })).toBeNull();
  });

  it('R2 clears the notice on the next successful refresh', async () => {
    await signInAs('Admin');
    let mode: 'first' | 'fail' | 'fresh' = 'first';
    const fresh = makeDashboard({ kpis: { ...makeDashboard().kpis!, revenue: { amount: '1300.00', currency: 'USD' } } });
    jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const { pathname } = new URL(String(input));
      if (pathname === '/api/v1/admin/dashboard') {
        if (mode === 'fail') return problemResponse(500, { title: 'Server error' });
        return jsonResponse(200, mode === 'fresh' ? fresh : makeDashboard());
      }
      return jsonResponse(200, makeRecentOrdersPage([]));
    });
    await renderAt('/admin');
    await screen.findByText('$1,234.50');
    await waitFor(() => expect(screen.getByRole('button', { name: 'Refresh' })).toBeEnabled());
    mode = 'fail';
    await fireEvent.press(screen.getByRole('button', { name: 'Refresh' }));
    await screen.findByText(/^Refresh failed:/);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Refresh' })).toBeEnabled());

    mode = 'fresh';
    await fireEvent.press(screen.getByRole('button', { name: 'Refresh' }));

    expect(await screen.findByText('$1,300.00')).toBeOnTheScreen();
    expect(screen.queryByText(/^Refresh failed:/)).toBeNull();
  });

  it('R3 never shows the other period figures when a period switch fails', async () => {
    await signInAs('Admin');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/v1/admin/dashboard') {
        return url.searchParams.get('period') === '30d'
          ? problemResponse(500, { title: 'Server error' })
          : jsonResponse(200, makeDashboard());
      }
      return jsonResponse(200, makeRecentOrdersPage([]));
    });
    await renderAt('/admin');
    await screen.findByText('$1,234.50');
    await waitFor(() => expect(screen.getByRole('button', { name: 'Refresh' })).toBeEnabled());

    await fireEvent.press(screen.getByRole('button', { name: '30 days' }));

    expect((await screen.findAllByRole('button', { name: 'Try again' })).length).toBeGreaterThan(0);
    expect(screen.queryByText('$1,234.50')).toBeNull();
    expect(screen.queryByText(/^Refresh failed:/)).toBeNull();
  });

  it('V1 shows the as-of line with period, currency and lag', async () => {
    await signInAs('Admin');
    mockApi();

    await renderAt('/admin');

    expect(await screen.findByText('Period: Today')).toBeOnTheScreen();
    expect(screen.getByText('Currency: USD')).toBeOnTheScreen();
    expect(screen.getByText('Lag: within target')).toBeOnTheScreen();
    expect(screen.getByText('Figures as of Nov 8, 2026, 14:30 UTC')).toBeOnTheScreen();
  });

  it('V2 shows how far behind the slowest projector is', async () => {
    await signInAs('Admin');
    mockApi(
      makeDashboard({
        needsAttention: { pendingReturns: 0, deadLetters: [], laggingProjectors: [{ name: 'orders-dashboard', lagSeconds: 130 }] },
      }),
    );

    await renderAt('/admin');

    expect(await screen.findByText('Lag: 130 s behind')).toBeOnTheScreen();
  });

  it('V3 names the period of the response, not the selected chip', async () => {
    await signInAs('Admin');
    mockApi(makeDashboard({ period: '30d' }));

    await renderAt('/admin');

    expect(await screen.findByText('Period: 30 days')).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Today' })).toBeSelected();
  });
});

describe('DashboardView greeting', () => {
  const renderGreeting = (now: () => Date) =>
    renderRouterWithQuery({ 'admin/index': () => <DashboardView now={now} /> }, { initialUrl: '/admin' });

  it('G3: Admin at 09:00 is greeted by name', async () => {
    await signInAs('Admin');
    mockApi();

    await renderGreeting(() => new Date(2026, 10, 12, 9, 0));

    expect(await screen.findByText('Good morning, Admin User')).toBeOnTheScreen();
  });

  it('G4: Support at 20:00 is greeted by name', async () => {
    await signInAs('Support');
    mockApi();

    await renderGreeting(() => new Date(2026, 10, 12, 20, 0));

    expect(await screen.findByText('Good evening, Support User')).toBeOnTheScreen();
  });
});
