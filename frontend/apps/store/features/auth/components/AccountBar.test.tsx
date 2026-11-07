import { fireEvent, screen, waitFor } from 'expo-router/testing-library';

import { endSession, getSessionSnapshot, resetSession } from '@/features/auth/session';
import type { UserRole } from '@/features/auth/types';
import { mockFetchRoutes } from '@/tests/fetch-mock';
import { createTestQueryClient, renderRouterWithQuery } from '@/tests/render-with-query';
import { signInAs } from '@/tests/session';

import { AccountBar } from './AccountBar';

jest.mock('expo-secure-store');

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const render = (client = createTestQueryClient()) =>
  renderRouterWithQuery({ index: AccountBar }, { initialUrl: '/', client });

describe('AccountBar', () => {
  it('shows nothing while the session is being restored', async () => {
    await render();

    expect(screen.queryByRole('link', { name: 'Log in' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Log out' })).toBeNull();
  });

  it('offers Log in and Register when signed out', async () => {
    await render();
    await endSession();

    expect(await screen.findByRole('link', { name: 'Log in' })).toBeOnTheScreen();
    expect(screen.getByRole('link', { name: 'Register' })).toBeOnTheScreen();
  });

  it.each<[UserRole, boolean]>([
    ['Customer', false],
    ['Support', true],
    ['Admin', true],
  ])('%s: the Admin link is %p', async (role, hasAdminLink) => {
    await signInAs(role);
    await render();

    expect(screen.getByText(`${role} User`)).toBeOnTheScreen();
    expect(screen.queryByRole('link', { name: 'Admin' }) !== null).toBe(hasAdminLink);
  });

  it('shows an Orders link when signed in and none when signed out', async () => {
    await render();
    await endSession();
    expect(await screen.findByRole('link', { name: 'Log in' })).toBeOnTheScreen();
    expect(screen.queryByRole('link', { name: 'Orders' })).toBeNull();

    await signInAs('Customer');

    expect(await screen.findByRole('link', { name: 'Orders' })).toBeOnTheScreen();
  });

  it('Log out calls the API, ends the session and clears the cache', async () => {
    const spy = mockFetchRoutes({ '/api/v1/auth/logout': () => new Response(null, { status: 204 }) });
    const client = createTestQueryClient();
    client.setQueryData(['admin', 'products'], { secret: true });
    await signInAs('Admin');
    await render(client);

    await fireEvent.press(screen.getByRole('button', { name: 'Log out' }));

    await waitFor(() => expect(getSessionSnapshot().status).toBe('signed-out'));
    expect(String(spy.mock.calls[0][0])).toContain('/api/v1/auth/logout');
    expect(client.getQueryData(['admin', 'products'])).toBeUndefined();
  });

  it('Log out still ends the session when the API call fails', async () => {
    jest.spyOn(globalThis, 'fetch').mockRejectedValue(new TypeError('Network request failed'));
    await signInAs('Customer');
    await render();

    await fireEvent.press(screen.getByRole('button', { name: 'Log out' }));

    await waitFor(() => expect(getSessionSnapshot().status).toBe('signed-out'));
  });
});
