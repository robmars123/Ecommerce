import { breadcrumbFor } from '@/features/admin-shell/breadcrumb';
import { ADMIN_NAV } from '@/features/admin-shell/nav';
import { adminRouteDecision } from '@/features/auth/admin-access';

const D = { label: 'Dashboard', href: '/admin' };
const GUID = '3f2b9c1e-7a44-4d0e-9a51-0c6f2d8e1b77';
const sections: Array<[string, string]> = [
  ['Orders', '/admin/orders'],
  ['Products', '/admin/products'],
  ['Categories', '/admin/categories'],
  ['Inventory', '/admin/inventory'],
  ['Returns', '/admin/returns'],
  ['Customers', '/admin/customers'],
];
const dynamic: Array<[string, string, string]> = [
  [`/admin/orders/${GUID}`, 'Orders', 'Order details'],
  [`/admin/products/${GUID}`, 'Products', 'Product details'],
  [`/admin/customers/${GUID}`, 'Customers', 'Customer details'],
];

describe('breadcrumbFor', () => {
  it('B1: /admin is the root crumb only', () => {
    expect(breadcrumbFor('/admin')).toEqual([D]);
  });

  it.each(sections)('B1: %s section', (label, href) => {
    expect(breadcrumbFor(href)).toEqual([D, { label, href }]);
  });

  it.each(dynamic)('B2: %s ends with the fixed detail label', (path, section, detail) => {
    const sectionHref = path.split('/').slice(0, 3).join('/');
    expect(breadcrumbFor(path)).toEqual([D, { label: section, href: sectionHref }, { label: detail, href: path }]);
  });

  it('B2: products/new is New product, not Product details', () => {
    expect(breadcrumbFor('/admin/products/new')).toEqual([
      D,
      { label: 'Products', href: '/admin/products' },
      { label: 'New product', href: '/admin/products/new' },
    ]);
  });

  it.each([
    '/',
    '',
    '/products',
    '/admin/foo',
    '/admin/orders/a/b',
    '/admin/categories/x',
    '/admin/inventory/x',
    '/admin/returns/x',
    '/admin//',
    '/administrator',
  ])('B3: unknown path %j gives no crumbs', (path) => {
    expect(breadcrumbFor(path)).toEqual([]);
  });

  it.each([
    ['/admin/', '/admin'],
    ['/admin/orders/', '/admin/orders'],
    ['/admin/orders/abc/', '/admin/orders/abc'],
  ])('B4: trailing slash %s equals %s', (withSlash, without) => {
    expect(breadcrumbFor(withSlash)).toEqual(breadcrumbFor(without));
    expect(breadcrumbFor(without).length).toBeGreaterThan(0);
  });

  it.each(dynamic)('B5: %s never shows the id as a label', (path) => {
    for (const crumb of breadcrumbFor(path)) expect(crumb.label).not.toContain(GUID);
  });

  it.each(['Admin', 'Support'] as const)('B6: %s never gets a crumb to a page it cannot open', (role) => {
    const paths = ['/admin', ...sections.map(([, href]) => href), ...dynamic.map(([path]) => path), '/admin/products/new'];
    for (const path of paths) {
      if (adminRouteDecision(role, path) !== null) continue;
      for (const crumb of breadcrumbFor(path)) expect(adminRouteDecision(role, crumb.href)).toBeNull();
    }
  });

  it('B7: every ready nav item is the section crumb from its own label and href', () => {
    const ready = ADMIN_NAV.flatMap((group) => group.items).filter((item) => item.ready);
    expect(ready.length).toBeGreaterThan(0);
    for (const item of ready) {
      expect(breadcrumbFor(item.href).slice(-1)[0]).toEqual({ label: item.label, href: item.href });
    }
  });
});
