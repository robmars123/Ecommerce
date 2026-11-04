import fs from 'node:fs';
import path from 'node:path';

import { fireEvent, renderRouter, screen } from 'expo-router/testing-library';

import { jsonResponse } from '@/tests/fetch-mock';
import { categories, makeDetail, makePage } from '@/tests/fixtures';

const APP_DIR = path.join(process.cwd(), 'app');

// file (relative to app/, "/" separators) -> url to render -> screen title -> built (real screen) or still a stub.
// Built screens fetch from the API, so their test mocks fetch and asserts the stub note is gone.
const ROUTES: ReadonlyArray<readonly [file: string, url: string, title: string, built: boolean]> = [
  ['index.tsx', '/', 'Home', true],
  ['products/index.tsx', '/products', 'Products', true],
  ['products/[slug].tsx', '/products/blue-shirt', 'Blue Shirt', true],
  ['cart.tsx', '/cart', 'Cart', false],
  ['checkout.tsx', '/checkout', 'Checkout', false],
  ['orders/index.tsx', '/orders', 'Orders', false],
  ['orders/[id].tsx', '/orders/42', 'Order', false],
  ['login.tsx', '/login', 'Log in', false],
  ['register.tsx', '/register', 'Register', false],
  ['reset-password.tsx', '/reset-password', 'Reset password', false],
  ['admin/index.tsx', '/admin', 'Admin dashboard', false],
  ['admin/products/index.tsx', '/admin/products', 'Admin · Products', false],
  ['admin/products/[id].tsx', '/admin/products/7', 'Admin · Product', false],
  ['admin/inventory.tsx', '/admin/inventory', 'Admin · Inventory', false],
  ['admin/orders/index.tsx', '/admin/orders', 'Admin · Orders', false],
  ['admin/orders/[id].tsx', '/admin/orders/9', 'Admin · Order', false],
  ['admin/returns.tsx', '/admin/returns', 'Admin · Returns', false],
  ['admin/customers/index.tsx', '/admin/customers', 'Admin · Customers', false],
  ['admin/customers/[id].tsx', '/admin/customers/3', 'Admin · Customer', false],
];

function mockCatalogApi() {
  return jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const { pathname } = new URL(String(input));
    if (pathname === '/api/v1/categories') return jsonResponse(200, { items: categories });
    if (pathname === '/api/v1/products/blue-shirt') return jsonResponse(200, makeDetail());
    if (pathname === '/api/v1/products') return jsonResponse(200, makePage([]));
    return new Response('not mocked', { status: 599 });
  });
}

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
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
      .filter((f) => f !== '_layout.tsx' && f !== '+not-found.tsx')
      .sort();
    const expected = ROUTES.map(([file]) => file).sort();
    expect(actual).toEqual(expected);
  });
});

describe('routes render under the real root layout', () => {
  it.each(ROUTES.filter(([, , , built]) => !built))(
    '%s renders at %s with title "%s" as a stub',
    async (_file, url, title) => {
      await renderRouter('./app', { initialUrl: url });

      expect(screen).toHavePathname(url);
      expect(screen.getAllByRole('header', { name: title }).length).toBeGreaterThanOrEqual(1);
      expect(screen.getByText('Not built yet.')).toBeOnTheScreen();
    },
  );

  it.each(ROUTES.filter(([, , , built]) => built))(
    '%s renders at %s as a real screen titled "%s"',
    async (_file, url, title) => {
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
