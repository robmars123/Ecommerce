import { act, renderRouter, screen } from 'expo-router/testing-library';

import { resetSession } from '@/features/auth/session';
import { makeDashboard, makeRecentOrdersPage } from '@/tests/admin-dashboard-fixtures';
import { makeAdminListPage } from '@/tests/admin-order-fixtures';
import { calledUrls, jsonResponse } from '@/tests/fetch-mock';
import { settleFetch } from '@/tests/settle';
import { signInAs } from '@/tests/session';
import type { Dashboard } from '@/features/admin-dashboard/types';

jest.mock('expo-secure-store');

const mockUseWindowDimensions = jest.fn();
jest.mock('react-native/Libraries/Utilities/useWindowDimensions', () => ({
  __esModule: true,
  default: () => mockUseWindowDimensions(),
}));

const DASHBOARD = '/api/v1/admin/dashboard';

function mockApi(nextDashboard: () => Dashboard) {
  return jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const { pathname } = new URL(String(input));
    if (pathname === DASHBOARD) return jsonResponse(200, nextDashboard());
    if (pathname === `${DASHBOARD}/recent-orders`) return jsonResponse(200, makeRecentOrdersPage([]));
    if (pathname === '/api/v1/admin/orders') return jsonResponse(200, makeAdminListPage([]));
    return new Response('not mocked', { status: 599 });
  });
}

const dashboardCalls = (spy: jest.SpyInstance) => calledUrls(spy).filter((u) => new URL(u).pathname === DASHBOARD);

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
  mockUseWindowDimensions.mockReturnValue({ width: 1024, height: 800, scale: 1, fontScale: 1 });
});

afterEach(() => {
  jest.useRealTimers();
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

describe('admin shell data (P2.11)', () => {
  it('L1: the Inventory badge shows the low-stock count, from the today dashboard', async () => {
    await signInAs('Admin');
    const spy = mockApi(() => makeDashboard({ lowStockCount: 4 }));

    await renderRouter('./app', { initialUrl: '/admin/orders' });

    expect(await screen.findByText('4 low')).toBeOnTheScreen();
    expect(calledUrls(spy).some((u) => u.includes('/admin/dashboard?period=today'))).toBe(true);
  });

  it('L2: on /admin the shell and the dashboard share one request', async () => {
    await signInAs('Admin');
    const spy = mockApi(() => makeDashboard());

    await renderRouter('./app', { initialUrl: '/admin' });
    expect(await screen.findByLabelText('Revenue')).toBeOnTheScreen();

    expect(dashboardCalls(spy)).toHaveLength(1);
  });

  it('L3: a Customer at /admin triggers no dashboard request', async () => {
    await signInAs('Customer');
    const spy = mockApi(() => makeDashboard());

    await renderRouter('./app', { initialUrl: '/admin' });

    expect(dashboardCalls(spy)).toHaveLength(0);
  });

  it('L4: the badge refreshes every 60 seconds', async () => {
    jest.useFakeTimers();
    await signInAs('Admin');
    let count = 4;
    const spy = mockApi(() => makeDashboard({ lowStockCount: count }));

    await renderRouter('./app', { initialUrl: '/admin/orders' });
    await settleFetch();
    expect(screen.getByText('4 low')).toBeOnTheScreen();
    count = 5;
    await act(() => jest.advanceTimersByTimeAsync(60_000));
    await settleFetch();

    expect(await screen.findByText('5 low')).toBeOnTheScreen();
    expect(dashboardCalls(spy)).toHaveLength(2);
  });
});

describe('admin shell attention card (P2.12)', () => {
  it('L5: Support sees failed events and no low-stock item', async () => {
    await signInAs('Support');
    mockApi(() =>
      makeDashboard({
        lowStockCount: 2,
        needsAttention: { pendingReturns: 0, deadLetters: [{ consumer: 'x', count: 1 }], laggingProjectors: [] },
      }),
    );

    await renderRouter('./app', { initialUrl: '/admin/orders' });

    expect(await screen.findByText('1 failed event')).toBeOnTheScreen();
    expect(screen.queryByText(/low-stock/)).toBeNull();
  });
});
