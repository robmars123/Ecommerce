import * as SecureStore from 'expo-secure-store';
import { fireEvent, renderRouter, screen, waitFor } from 'expo-router/testing-library';

import { endSession, getSessionSnapshot, resetSession } from '@/features/auth/session';
import type { UserRole } from '@/features/auth/types';
import { makeDashboard, makeRecentOrdersPage } from '@/tests/admin-dashboard-fixtures';
import { makeAdminListPage } from '@/tests/admin-order-fixtures';
import { jsonResponse, mockFetchRoutes } from '@/tests/fetch-mock';
import { signInAs } from '@/tests/session';

jest.mock('expo-secure-store');

const store = SecureStore as jest.Mocked<typeof SecureStore>;
// Loading the whole app/ module tree cold goes over the 5 s default under parallel load (same note as routes.test.tsx).
jest.setTimeout(15_000);

const mockUseWindowDimensions = jest.fn();
jest.mock('react-native/Libraries/Utilities/useWindowDimensions', () => ({
  __esModule: true,
  default: () => mockUseWindowDimensions(),
}));

const LOGOUT = '/api/v1/auth/logout';

function mockApi() {
  return mockFetchRoutes({
    '/api/v1/cart': () => jsonResponse(200, { items: [] }),
    '/api/v1/admin/dashboard/recent-orders': () => jsonResponse(200, makeRecentOrdersPage([])),
    '/api/v1/admin/dashboard': () => jsonResponse(200, makeDashboard()),
    '/api/v1/admin/orders': () => jsonResponse(200, makeAdminListPage([])),
    [LOGOUT]: () => new Response(null, { status: 204 }),
  });
}

const callsTo = (spy: jest.SpyInstance, path: string) => spy.mock.calls.filter((c) => String(c[0]).includes(path));

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
  mockUseWindowDimensions.mockReturnValue({ width: 1024, height: 800, scale: 1, fontScale: 1 });
});

afterEach(() => {
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

describe('account page (P2.85)', () => {
  it('A1: signed out goes to sign-in with returnTo=/account and shows no account details', async () => {
    mockApi();
    await endSession();
    await renderRouter('./app', { initialUrl: '/account' });

    await waitFor(() => expect(screen).toHavePathname('/login'));
    expect(screen).toHaveSearchParams({ returnTo: '/account' });
    expect(screen.queryByRole('header', { name: 'Account' })).toBeNull();
    expect(screen.queryByText(/@example\.test/)).toBeNull();
  });

  it.each<UserRole>(['Customer', 'Support', 'Admin'])('A2: %s sees name, email and role, with no /auth/me call', async (role) => {
    const spy = mockApi();
    await signInAs(role);
    await renderRouter('./app', { initialUrl: '/account' });

    expect((await screen.findAllByRole('header', { name: 'Account' })).length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText(`${role} User`)).toHaveLength(2);
    expect(screen.getAllByText(`${role.toLowerCase()}@example.test`)).toHaveLength(1);
    // Admin also has an "Admin" link in the top bar.
    expect(screen.getAllByText(role)).toHaveLength(role === 'Admin' ? 2 : 1);
    expect(screen).toHavePathname('/account');
    expect(callsTo(spy, '/api/v1/auth/me')).toHaveLength(0);
  });

  it('A3: a blank display name shows "Not set" and the email', async () => {
    mockApi();
    await signInAs('Customer', { user: { id: 'u-1', email: 'customer@example.test', displayName: '  ', role: 'Customer' } });
    await renderRouter('./app', { initialUrl: '/account' });

    expect(await screen.findByText('Not set')).toBeOnTheScreen();
    // The top-bar label falls back to the email for a blank name, so the email shows twice.
    expect(screen.getAllByText('customer@example.test').length).toBeGreaterThanOrEqual(2);
  });

  it('A4: Log out posts once, ends the session and lands on /login', async () => {
    const spy = mockApi();
    await signInAs('Customer');
    await renderRouter('./app', { initialUrl: '/account' });

    const buttons = await screen.findAllByRole('button', { name: 'Log out' });
    await fireEvent.press(buttons[buttons.length - 1]);

    await waitFor(() => expect(screen).toHavePathname('/login'));
    expect(callsTo(spy, LOGOUT)).toHaveLength(1);
    expect(getSessionSnapshot().status).toBe('signed-out');
  });

  it('A5: while the session is restoring it shows the loading state and does not redirect', async () => {
    mockApi();
    // The root layout restores the session at start; a stored-token read that never answers keeps the status at 'restoring'.
    store.getItemAsync.mockImplementation(() => new Promise(() => undefined));
    await renderRouter('./app', { initialUrl: '/account' });

    expect(screen.getByLabelText('Loading')).toBeOnTheScreen();
    expect(screen).toHavePathname('/account');
    expect(screen.queryByRole('header', { name: 'Account' })).toBeNull();
  });

  it.each<'Admin' | 'Support'>(['Admin', 'Support'])('A6: the %s sidebar footer links to /account', async (role) => {
    mockApi();
    await signInAs(role);
    await renderRouter('./app', { initialUrl: '/admin/orders' });

    const link = await screen.findByRole('link', { name: `Signed in as ${role} User, ${role}` });
    await fireEvent.press(link);

    await waitFor(() => expect(screen).toHavePathname('/account'));
    expect((await screen.findAllByRole('header', { name: 'Account' })).length).toBeGreaterThanOrEqual(1);
  });
});
