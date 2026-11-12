import { renderRouter, screen } from 'expo-router/testing-library';

import { resetSession } from '@/features/auth/session';
import { makeDashboard, makeRecentOrder, makeRecentOrdersPage } from '@/tests/admin-dashboard-fixtures';
import { makeAdminItem, makeAdminPage, makeAdminProduct } from '@/tests/admin-fixtures';
import { makeCustomerDetail, makeCustomerItem, makeCustomerPage } from '@/tests/admin-customer-fixtures';
import { makeAdminListItem, makeAdminListPage, makeAdminOrderDetail, makeReturnPage, makeReturnRow } from '@/tests/admin-order-fixtures';
import { jsonResponse } from '@/tests/fetch-mock';
import { makeInventoryItem, makeInventoryPage } from '@/tests/fixtures';
import { signInAs } from '@/tests/session';

jest.mock('expo-secure-store');

const mockUseWindowDimensions = jest.fn();
jest.mock('react-native/Libraries/Utilities/useWindowDimensions', () => ({
  __esModule: true,
  default: () => mockUseWindowDimensions(),
}));

// P2.46: every admin route mounts in the phone shell with long content. Overflow itself is checked by hand (plan, manual check 1).
const LONG = 'L'.repeat(100);

function mockAdminApi() {
  return jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const { pathname } = new URL(String(input));
    if (pathname === '/api/v1/admin/orders') {
      return jsonResponse(
        200,
        makeAdminListPage([makeAdminListItem({ customerEmail: `${LONG}@example.test` })], { totalPages: 2, totalCount: 40 }),
      );
    }
    if (pathname === '/api/v1/admin/orders/9') return jsonResponse(200, makeAdminOrderDetail());
    if (pathname === '/api/v1/admin/customers') {
      return jsonResponse(
        200,
        makeCustomerPage([makeCustomerItem({ displayName: LONG, email: `${LONG}@example.test` })], { totalPages: 2, totalCount: 40 }),
      );
    }
    if (pathname === '/api/v1/admin/customers/3')
      return jsonResponse(200, makeCustomerDetail({ displayName: LONG, email: `${LONG}@example.test` }));
    if (pathname === '/api/v1/admin/returns') {
      return jsonResponse(200, makeReturnPage([makeReturnRow({ reason: LONG })], { totalPages: 2, totalCount: 40 }));
    }
    if (pathname === '/api/v1/admin/dashboard') return jsonResponse(200, makeDashboard());
    if (pathname === '/api/v1/admin/dashboard/recent-orders') {
      return jsonResponse(200, makeRecentOrdersPage([makeRecentOrder({ customerEmail: `${LONG}@example.test` })], { totalCount: 40 }));
    }
    if (pathname === '/api/v1/admin/inventory') {
      return jsonResponse(200, makeInventoryPage([makeInventoryItem({ productName: LONG, sku: LONG })], { totalPages: 2, totalCount: 40 }));
    }
    if (pathname === '/api/v1/admin/products') {
      return jsonResponse(200, makeAdminPage([makeAdminItem({ name: LONG })], { totalPages: 2, totalCount: 40 }));
    }
    if (pathname.startsWith('/api/v1/admin/products/')) return jsonResponse(200, makeAdminProduct({ name: LONG }));
    if (pathname === '/api/v1/categories' || pathname === '/api/v1/admin/categories') {
      return jsonResponse(200, { items: [{ slug: 'long-category', name: LONG, parentSlug: null }] });
    }
    return new Response('not mocked', { status: 599 });
  });
}

// url, title, paged
const ROUTES: [string, string, boolean][] = [
  ['/admin', 'Admin dashboard', true],
  ['/admin/orders', 'Orders', true],
  ['/admin/orders/9', 'Order', false],
  ['/admin/returns', 'Returns', true],
  ['/admin/products', 'Admin · Products', true],
  ['/admin/products/new', 'Admin · New product', false],
  ['/admin/products/7', 'Admin · Product', false],
  ['/admin/categories', 'Admin · Categories', false],
  ['/admin/inventory', 'Admin · Inventory', true],
  ['/admin/customers', 'Admin · Customers', true],
  ['/admin/customers/3', 'Admin · Customer', false],
];

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
  mockUseWindowDimensions.mockReturnValue({ width: 375, height: 800, scale: 1, fontScale: 1 });
});

afterEach(() => {
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

describe('admin routes at 375 px', () => {
  it.each(ROUTES)('PW1: %s mounts in the phone shell with long content', async (url, title, paged) => {
    await signInAs('Admin');
    mockAdminApi();
    await renderRouter('./app', { initialUrl: url });

    expect((await screen.findAllByRole('header', { name: title })).length).toBeGreaterThanOrEqual(1);
    expect(screen.getByRole('button', { name: 'Open menu' })).toBeOnTheScreen();
    expect(screen.queryByLabelText('Admin navigation')).toBeNull();
    expect(screen.getByRole('switch', { name: 'Dark mode' })).toBeOnTheScreen();
    if (paged) {
      expect(await screen.findByRole('button', { name: /^Previous/ })).toBeOnTheScreen();
      expect(screen.getByRole('button', { name: /^Next/ })).toBeOnTheScreen();
    }
  });
});
