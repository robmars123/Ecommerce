import { fireEvent, screen } from 'expo-router/testing-library';
import { Text } from 'react-native';

import { makeAdminItem, makeAdminPage } from '@/tests/admin-fixtures';
import { jsonResponse } from '@/tests/fetch-mock';
import { categories } from '@/tests/fixtures';
import { renderRouterWithQuery } from '@/tests/render-with-query';

import { AdminProductListView } from './AdminProductListView';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const render = (url = '/admin/products') =>
  renderRouterWithQuery(
    {
      'admin/products/index': AdminProductListView,
      'admin/products/[id]': () => <Text>Detail screen</Text>,
      'admin/products/new': () => <Text>New screen</Text>,
    },
    { initialUrl: url },
  );

function mockApi(page = makeAdminPage([makeAdminItem(), makeAdminItem({ id: 'a-2', name: 'Wool coat', isActive: true })])) {
  return jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const { pathname } = new URL(String(input));
    if (pathname === '/api/v1/categories') return jsonResponse(200, { items: categories });
    if (pathname === '/api/v1/admin/products') return jsonResponse(200, page);
    return new Response('not mocked', { status: 599 });
  });
}

const adminUrls = (spy: jest.SpyInstance) => spy.mock.calls.map((c) => String(c[0])).filter((u) => u.includes('/admin/products'));

describe('AdminProductListView', () => {
  it('lists products, says which are published, and links each row to its detail page', async () => {
    mockApi();
    await render();

    const row = await screen.findByRole('link', { name: /Wool coat/ });
    expect(row).toBeOnTheScreen();
    expect(screen.getByRole('link', { name: /Linen shirt · Not published/ })).toBeOnTheScreen();

    await fireEvent.press(row);

    expect(screen).toHavePathname('/admin/products/a-2');
  });

  it('reads the filters from the URL and sends them to the API', async () => {
    const spy = mockApi();
    await render('/admin/products?status=inactive&category=shirts&page=2&search=linen');

    await screen.findByRole('link', { name: /Wool coat/ });

    const url = adminUrls(spy)[0];
    expect(url).toContain('status=inactive');
    expect(url).toContain('category=shirts');
    expect(url).toContain('page=2');
    expect(url).toContain('search=linen');
  });

  it('choosing a status puts it in the URL and refetches', async () => {
    const spy = mockApi();
    await render();
    await screen.findByRole('link', { name: /Wool coat/ });

    await fireEvent.press(screen.getByRole('button', { name: 'Active' }));

    expect(screen).toHaveSearchParams({ status: 'active' });
    await screen.findByRole('link', { name: /Wool coat/ });
    expect(adminUrls(spy).some((u) => u.includes('status=active'))).toBe(true);
  });

  it('has an Add product link', async () => {
    mockApi();
    await render();

    await fireEvent.press(await screen.findByRole('link', { name: 'Add product' }));

    expect(screen).toHavePathname('/admin/products/new');
  });

  it('shows an empty state when nothing matches', async () => {
    mockApi(makeAdminPage([]));
    await render();

    expect(await screen.findByText('No products match.')).toBeOnTheScreen();
  });
});
