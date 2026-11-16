import { QueryClient } from '@tanstack/react-query';
import { act, fireEvent, renderRouter, screen, waitFor } from 'expo-router/testing-library';

import { getSessionSnapshot, resetSession } from '@/features/auth/session';
import { makeDashboard, makeRecentOrdersPage } from '@/tests/admin-dashboard-fixtures';
import { makeAdminListPage } from '@/tests/admin-order-fixtures';
import { jsonResponse, mockFetchRoutes, problemResponse } from '@/tests/fetch-mock';
import { signInAs } from '@/tests/session';

jest.mock('expo-secure-store');

const mockUseWindowDimensions = jest.fn();
jest.mock('react-native/Libraries/Utilities/useWindowDimensions', () => ({
  __esModule: true,
  default: () => mockUseWindowDimensions(),
}));

const LOGOUT = '/api/v1/auth/logout';

function mockApi(logoutRoute: () => Response | Promise<Response>) {
  return mockFetchRoutes({
    '/api/v1/admin/dashboard/recent-orders': () => jsonResponse(200, makeRecentOrdersPage([])),
    '/api/v1/admin/dashboard': () => jsonResponse(200, makeDashboard()),
    '/api/v1/admin/orders': () => jsonResponse(200, makeAdminListPage([])),
    [LOGOUT]: logoutRoute as () => Response,
  });
}

const logoutCalls = (spy: jest.SpyInstance) => spy.mock.calls.filter((c) => String(c[0]).includes(LOGOUT));

async function openAdminOrders(role: 'Admin' | 'Support') {
  await signInAs(role);
  await renderRouter('./app', { initialUrl: '/admin/orders' });
  return await screen.findByRole('button', { name: 'Log out' });
}

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
  mockUseWindowDimensions.mockReturnValue({ width: 1024, height: 800, scale: 1, fontScale: 1 });
});

afterEach(() => {
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

describe('admin logout (P2.84)', () => {
  it('O4: Log out posts once, ends the session, clears the cache and lands on /login', async () => {
    const spy = mockApi(() => new Response(null, { status: 204 }));
    const clearSpy = jest.spyOn(QueryClient.prototype, 'clear');
    await openAdminOrders('Admin');

    await fireEvent.press(screen.getByRole('button', { name: 'Log out' }));

    await waitFor(() => expect(screen).toHavePathname('/login'));
    expect(logoutCalls(spy)).toHaveLength(1);
    expect(logoutCalls(spy)[0][1]).toMatchObject({ method: 'POST' });
    expect(getSessionSnapshot().status).toBe('signed-out');
    // The gate first redirects with /admin/orders, then the unmounting page stack re-runs it at /admin (see report, Concerns).
    expect(screen).toHaveSearchParams({ returnTo: '/admin' });
    expect(screen.queryByLabelText('Admin navigation')).toBeNull();
    // Two clears: the root layout's signed-out hook, and the logout mutation's own (arch §10: every cached admin query is dropped).
    expect(clearSpy).toHaveBeenCalledTimes(2);
  });

  it.each<[string, () => Response]>([
    [
      'the network fails',
      () => {
        throw new TypeError('Network request failed');
      },
    ],
    ['the API answers 500', () => problemResponse(500, { title: 'Server error' })],
  ])('O5: when %s, the session still ends and the admin lands on /login', async (_name, logoutRoute) => {
    mockApi(logoutRoute);
    await openAdminOrders('Admin');

    await fireEvent.press(screen.getByRole('button', { name: 'Log out' }));

    await waitFor(() => expect(screen).toHavePathname('/login'));
    expect(getSessionSnapshot().status).toBe('signed-out');
  });

  it('O6: a double press while the request is open sends one logout call', async () => {
    let release: () => void = () => undefined;
    const held = new Promise<Response>((resolve) => {
      release = () => resolve(new Response(null, { status: 204 }));
    });
    const spy = mockApi(() => held as unknown as Response);
    await openAdminOrders('Admin');

    await fireEvent.press(screen.getByRole('button', { name: 'Log out' }));
    // The second tap comes a frame later, once the pending state has rendered.
    await waitFor(() => expect(screen.getByRole('button', { name: 'Log out' })).toBeDisabled());
    await fireEvent.press(screen.getByRole('button', { name: 'Log out' }));
    expect(logoutCalls(spy)).toHaveLength(1);

    await act(async () => release());

    await waitFor(() => expect(getSessionSnapshot().status).toBe('signed-out'));
    expect(logoutCalls(spy)).toHaveLength(1);
  });

  it('O7: Support has the same Log out button and it works the same', async () => {
    const spy = mockApi(() => new Response(null, { status: 204 }));
    await openAdminOrders('Support');

    await fireEvent.press(screen.getByRole('button', { name: 'Log out' }));

    await waitFor(() => expect(screen).toHavePathname('/login'));
    expect(logoutCalls(spy)).toHaveLength(1);
    expect(getSessionSnapshot().status).toBe('signed-out');
  });
});
