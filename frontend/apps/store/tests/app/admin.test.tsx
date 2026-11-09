import * as SecureStore from 'expo-secure-store';
import { renderRouter, screen, waitFor } from 'expo-router/testing-library';

import { resetSession } from '@/features/auth/session';
import type { UserRole } from '@/features/auth/types';
import { makeAdminItem, makeAdminPage } from '@/tests/admin-fixtures';
import { jsonResponse } from '@/tests/fetch-mock';
import { categories } from '@/tests/fixtures';
import { signInAs } from '@/tests/session';

jest.mock('expo-secure-store');

const store = SecureStore as jest.Mocked<typeof SecureStore>;

function mockApi() {
  return jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const { pathname } = new URL(String(input));
    if (pathname === '/api/v1/categories') return jsonResponse(200, { items: categories });
    if (pathname === '/api/v1/admin/products') return jsonResponse(200, makeAdminPage([makeAdminItem({ name: 'Wool coat' })]));
    return new Response('not mocked', { status: 599 });
  });
}

const adminCalls = (spy: jest.SpyInstance) => spy.mock.calls.filter((c) => String(c[0]).includes('/api/v1/admin'));

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
  store.getItemAsync.mockResolvedValue(null);
});

afterEach(() => {
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

describe('admin area gate (F7b)', () => {
  it('signed out: /admin/products goes to login with a returnTo', async () => {
    const spy = mockApi();
    await renderRouter('./app', { initialUrl: '/admin/products' });

    await waitFor(() => expect(screen).toHavePathname('/login'));
    expect(screen).toHaveSearchParams({ returnTo: '/admin/products' });
    expect(adminCalls(spy)).toHaveLength(0);
  });

  it('Customer: sent home and no admin API call is made', async () => {
    const spy = mockApi();
    await signInAs('Customer');
    await renderRouter('./app', { initialUrl: '/admin/products' });

    await waitFor(() => expect(screen).toHavePathname('/products'));
    expect(adminCalls(spy)).toHaveLength(0);
  });

  it('Support: may open orders but is sent to the dashboard from products', async () => {
    mockApi();
    await signInAs('Support');
    await renderRouter('./app', { initialUrl: '/admin/orders' });

    expect(screen).toHavePathname('/admin/orders');
    expect(screen.getAllByRole('header', { name: 'Orders' }).length).toBeGreaterThanOrEqual(1);
  });

  it('Support: /admin/products redirects to /admin and makes no admin API call', async () => {
    const spy = mockApi();
    await signInAs('Support');
    await renderRouter('./app', { initialUrl: '/admin/products' });

    await waitFor(() => expect(screen).toHavePathname('/admin'));
    expect(adminCalls(spy).filter((c) => !String(c[0]).includes('/api/v1/admin/dashboard'))).toHaveLength(0); // the dashboard it lands on is allowed to load
  });

  it('Admin: the product list loads', async () => {
    mockApi();
    await signInAs('Admin');
    await renderRouter('./app', { initialUrl: '/admin/products' });

    expect(await screen.findByRole('link', { name: /Wool coat/ })).toBeOnTheScreen();
    expect(screen).toHavePathname('/admin/products');
  });

  it('while the session is restoring: a loading state, no redirect and no fetch', async () => {
    const spy = mockApi();
    store.getItemAsync.mockImplementation(() => new Promise(() => undefined));
    await renderRouter('./app', { initialUrl: '/admin/products' });

    expect(screen.getByLabelText('Loading')).toBeOnTheScreen();
    expect(screen).toHavePathname('/admin/products');
    expect(spy).not.toHaveBeenCalled();
  });
});

describe('account bar by role on the home screen', () => {
  it.each<[UserRole, boolean]>([
    ['Customer', false],
    ['Support', true],
    ['Admin', true],
  ])('%s sees the Admin link: %p', async (role, visible) => {
    mockApi();
    await signInAs(role);
    await renderRouter('./app', { initialUrl: '/' });

    expect(await screen.findByRole('button', { name: 'Log out' })).toBeOnTheScreen();
    expect(screen.queryByRole('link', { name: 'Admin' }) !== null).toBe(visible);
  });

  it('signed out sees Log in and Register', async () => {
    mockApi();
    await renderRouter('./app', { initialUrl: '/' });

    expect(await screen.findByRole('link', { name: 'Log in' })).toBeOnTheScreen();
    expect(screen.getByRole('link', { name: 'Register' })).toBeOnTheScreen();
  });
});
