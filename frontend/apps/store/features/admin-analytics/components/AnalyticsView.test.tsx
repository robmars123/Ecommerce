import { fireEvent, screen, waitFor } from 'expo-router/testing-library';

import { resetSession } from '@/features/auth/session';
import { makeDashboard, makeRecentOrdersPage, makeRevenueTrend } from '@/tests/admin-dashboard-fixtures';
import { calledUrls, jsonResponse } from '@/tests/fetch-mock';
import { renderRouterWithQuery } from '@/tests/render-with-query';
import { signInAs } from '@/tests/session';

import AdminLayout from '../../../app/admin/_layout';
import AdminAnalyticsScreen from '../../../app/admin/analytics';

jest.mock('expo-secure-store');

// P2-tier4 D15: /admin/analytics shows the trend and top products for the URL period. Admin only.

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

function mockApi() {
  return jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const { pathname } = new URL(String(input));
    if (pathname === '/api/v1/admin/dashboard') return jsonResponse(200, makeDashboard());
    if (pathname === '/api/v1/admin/dashboard/trend') return jsonResponse(200, makeRevenueTrend({ period: '7d' }));
    if (pathname === '/api/v1/admin/dashboard/recent-orders') return jsonResponse(200, makeRecentOrdersPage([]));
    return new Response('not mocked', { status: 599 });
  });
}

function renderAt(initialUrl: string) {
  return renderRouterWithQuery(
    { 'admin/_layout': AdminLayout, 'admin/index': () => null, 'admin/analytics': AdminAnalyticsScreen },
    { initialUrl },
  );
}

describe('AnalyticsView', () => {
  it('shows the trend and the top products for the period in the URL', async () => {
    await signInAs('Admin');
    const spy = mockApi();

    await renderAt('/admin/analytics?period=7d');

    expect(await screen.findByText('$2,345.60')).toBeOnTheScreen();
    expect(screen.getByTestId('revenue-trend-chart')).toBeOnTheScreen();
    expect(await screen.findByText('Blue Shirt · 9 sold')).toBeOnTheScreen();
    expect(calledUrls(spy).some((u) => u.includes('/trend?period=7d'))).toBe(true);
  });

  it('writes the chosen period to the URL and asks the server for it', async () => {
    await signInAs('Admin');
    const spy = mockApi();
    await renderAt('/admin/analytics');
    await screen.findByText('$2,345.60');

    await fireEvent.press(screen.getByRole('button', { name: '30 days' }));

    await waitFor(() => expect(screen).toHaveSearchParams({ period: '30d' }));
    await waitFor(() => expect(calledUrls(spy).some((u) => u.includes('/trend?period=30d'))).toBe(true));
  });

  it('sends Support to the dashboard with no trend request', async () => {
    await signInAs('Support');
    const spy = mockApi();

    await renderAt('/admin/analytics');

    await waitFor(() => expect(screen).toHavePathname('/admin'));
    expect(calledUrls(spy).filter((u) => u.includes('/trend'))).toEqual([]);
  });
});
