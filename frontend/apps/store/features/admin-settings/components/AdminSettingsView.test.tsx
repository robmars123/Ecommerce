import { fireEvent, screen, waitFor } from 'expo-router/testing-library';

import { resetSession } from '@/features/auth/session';
import { calledUrls, jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { makeDashboard, makeRecentOrdersPage } from '@/tests/admin-dashboard-fixtures';
import { renderRouterWithQuery } from '@/tests/render-with-query';
import { signInAs } from '@/tests/session';

import AdminLayout from '../../../app/admin/_layout';
import AdminSettingsScreen from '../../../app/admin/settings';

jest.mock('expo-secure-store');

// P2-tier4: the read-only settings page. Admin only; Support is sent to the dashboard and never requests it.

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

function mockApi(settings: () => Response) {
  return jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const { pathname } = new URL(String(input));
    if (pathname === '/api/v1/admin/settings') return settings();
    if (pathname === '/api/v1/admin/dashboard') return jsonResponse(200, makeDashboard({ kpis: null }));
    if (pathname === '/api/v1/admin/dashboard/recent-orders') return jsonResponse(200, makeRecentOrdersPage([]));
    return new Response('not mocked', { status: 599 });
  });
}

function renderAt(initialUrl: string) {
  return renderRouterWithQuery(
    { 'admin/_layout': AdminLayout, 'admin/index': () => null, 'admin/settings': AdminSettingsScreen },
    { initialUrl },
  );
}

describe('AdminSettingsView', () => {
  it('shows the currency and the low-stock threshold the server returned, read-only', async () => {
    await signInAs('Admin');
    mockApi(() => jsonResponse(200, { currency: 'USD', lowStockThreshold: 3 }));

    await renderAt('/admin/settings');

    expect(await screen.findByText('USD')).toBeOnTheScreen();
    expect(screen.getByText('3 units available or fewer')).toBeOnTheScreen();
    expect(screen.getByText('Read-only. Set in server configuration.')).toBeOnTheScreen();
  });

  it('shows the error and a retry that loads the settings', async () => {
    await signInAs('Admin');
    let fail = true;
    mockApi(() => (fail ? problemResponse(404, {}) : jsonResponse(200, { currency: 'USD', lowStockThreshold: 3 })));
    await renderAt('/admin/settings');

    const retry = await screen.findByRole('button', { name: 'Try again' });
    fail = false;
    await fireEvent.press(retry);

    expect(await screen.findByText('USD')).toBeOnTheScreen();
  });

  it('sends Support to the dashboard without requesting the settings', async () => {
    await signInAs('Support');
    const spy = mockApi(() => jsonResponse(200, { currency: 'USD', lowStockThreshold: 3 }));

    await renderAt('/admin/settings');

    await waitFor(() => expect(screen).toHavePathname('/admin'));
    expect(calledUrls(spy).filter((u) => u.includes('/admin/settings'))).toEqual([]);
  });
});
