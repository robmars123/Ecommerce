import fs from 'node:fs';
import path from 'node:path';

import { fireEvent, renderRouter, screen } from 'expo-router/testing-library';

import { resetSession } from '@/features/auth/session';
import type { UserRole } from '@/features/auth/types';
import { makeAdminPage, makeAdminProduct } from '@/tests/admin-fixtures';
import { jsonResponse } from '@/tests/fetch-mock';
import { categories, makeDetail, makePage } from '@/tests/fixtures';
import { signInAs } from '@/tests/session';

jest.mock('expo-secure-store');

const APP_DIR = path.join(process.cwd(), 'app');

// file (relative to app/, "/" separators) -> url to render -> screen title -> built (real screen) or still a stub -> who is signed in.
// Built screens fetch from the API, so their test mocks fetch and asserts the stub note is gone. /admin pages need an Admin session.
const ROUTES: ReadonlyArray<readonly [file: string, url: string, title: string, built: boolean, session: UserRole | null]> = [
  ['index.tsx', '/', 'Home', true, null],
  ['products/index.tsx', '/products', 'Products', true, null],
  ['products/[slug].tsx', '/products/blue-shirt', 'Blue Shirt', true, null],
  ['cart.tsx', '/cart', 'Cart', false, null],
  ['checkout.tsx', '/checkout', 'Checkout', false, null],
  ['orders/index.tsx', '/orders', 'Orders', false, null],
  ['orders/[id].tsx', '/orders/42', 'Order', false, null],
  ['login.tsx', '/login', 'Log in', true, null],
  ['register.tsx', '/register', 'Register', true, null],
  ['reset-password.tsx', '/reset-password', 'Reset password', true, null],
  ['admin/index.tsx', '/admin', 'Admin dashboard', false, 'Admin'],
  ['admin/products/index.tsx', '/admin/products', 'Admin · Products', true, 'Admin'],
  ['admin/products/[id].tsx', '/admin/products/7', 'Admin · Product', true, 'Admin'],
  ['admin/products/new.tsx', '/admin/products/new', 'Admin · New product', true, 'Admin'],
  ['admin/inventory.tsx', '/admin/inventory', 'Admin · Inventory', false, 'Admin'],
  ['admin/orders/index.tsx', '/admin/orders', 'Admin · Orders', false, 'Admin'],
  ['admin/orders/[id].tsx', '/admin/orders/9', 'Admin · Order', false, 'Admin'],
  ['admin/returns.tsx', '/admin/returns', 'Admin · Returns', false, 'Admin'],
  ['admin/customers/index.tsx', '/admin/customers', 'Admin · Customers', false, 'Admin'],
  ['admin/customers/[id].tsx', '/admin/customers/3', 'Admin · Customer', false, 'Admin'],
];

function mockCatalogApi() {
  return jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const { pathname } = new URL(String(input));
    if (pathname === '/api/v1/categories') return jsonResponse(200, { items: categories });
    if (pathname === '/api/v1/products/blue-shirt') return jsonResponse(200, makeDetail());
    if (pathname === '/api/v1/products') return jsonResponse(200, makePage([]));
    if (pathname === '/api/v1/admin/products') return jsonResponse(200, makeAdminPage([]));
    if (pathname.startsWith('/api/v1/admin/products/')) return jsonResponse(200, makeAdminProduct());
    return new Response('not mocked', { status: 599 });
  });
}

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

function listRouteFiles(dir: string, prefix = ''): string[] {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const rel = prefix ? `${prefix}/${entry.name}` : entry.name;
    return entry.isDirectory() ? listRouteFiles(path.join(dir, entry.name), rel) : [rel];
  });
}

describe('route table', () => {
  it('matches the files under app/ (excluding _layout and +not-found)', () => {
    const actual = listRouteFiles(APP_DIR)
      .filter((f) => !f.endsWith('_layout.tsx') && f !== '+not-found.tsx')
      .sort();
    const expected = ROUTES.map(([file]) => file).sort();
    expect(actual).toEqual(expected);
  });
});

describe('routes render under the real root layout', () => {
  it.each(ROUTES.filter(([, , , built]) => !built))(
    '%s renders at %s with title "%s" as a stub',
    async (_file, url, title, _built, session) => {
      if (session) await signInAs(session);
      await renderRouter('./app', { initialUrl: url });

      expect(screen).toHavePathname(url);
      expect(screen.getAllByRole('header', { name: title }).length).toBeGreaterThanOrEqual(1);
      expect(screen.getByText('Not built yet.')).toBeOnTheScreen();
    },
  );

  it.each(ROUTES.filter(([, , , built]) => built))(
    '%s renders at %s as a real screen titled "%s"',
    async (_file, url, title, _built, session) => {
      if (session) await signInAs(session);
      mockCatalogApi();
      await renderRouter('./app', { initialUrl: url });

      expect(screen).toHavePathname(url);
      expect((await screen.findAllByRole('header', { name: title })).length).toBeGreaterThanOrEqual(1);
      expect(screen.queryByText('Not built yet.')).toBeNull();
    },
  );
});

describe('not-found', () => {
  it('renders the not-found screen for an unknown URL', async () => {
    await renderRouter('./app', { initialUrl: '/does-not-exist' });

    expect(screen.getAllByRole('header', { name: 'Not found' }).length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('This screen does not exist.')).toBeOnTheScreen();
  });

  it('"Go home" recovers to the home screen', async () => {
    await renderRouter('./app', { initialUrl: '/does-not-exist' });

    await fireEvent.press(screen.getByRole('link', { name: 'Go home' }));

    expect(screen).toHavePathname('/');
    expect(screen.getAllByRole('header', { name: 'Home' }).length).toBeGreaterThanOrEqual(1);
  });
});
