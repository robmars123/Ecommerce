import { ADMIN_NAV, visibleNav } from './nav';

const labels = (groups: { items: { label: string }[] }[]) => groups.map((g) => g.items.map((i) => i.label));

describe('admin nav', () => {
  it('N1: the table has the planned groups, order, labels, hrefs and ready flags', () => {
    expect(ADMIN_NAV).toEqual([
      {
        title: 'Overview',
        items: [
          { label: 'Dashboard', href: '/admin', ready: true },
          { label: 'User Stories', href: '/admin', ready: false },
        ],
      },
      {
        title: 'Manage',
        items: [
          { label: 'Orders', href: '/admin/orders', ready: true },
          { label: 'Products', href: '/admin/products', ready: true },
          { label: 'Categories', href: '/admin/categories', ready: true },
          { label: 'Inventory', href: '/admin/inventory', ready: true },
          { label: 'Returns', href: '/admin/returns', ready: true },
          { label: 'Customers', href: '/admin/customers', ready: true },
        ],
      },
      {
        title: 'Insights',
        items: [
          { label: 'Analytics', href: '/admin/analytics', ready: false },
          { label: 'Settings', href: '/admin/settings', ready: false },
        ],
      },
    ]);
  });

  it('N2: Support sees Dashboard, User Stories, Orders, Customers and no Insights group', () => {
    const groups = visibleNav('Support');

    expect(groups.map((g) => g.title)).toEqual(['Overview', 'Manage']);
    expect(labels(groups)).toEqual([
      ['Dashboard', 'User Stories'],
      ['Orders', 'Customers'],
    ]);
  });

  it('N3: Admin sees all 3 groups and all 10 items', () => {
    const groups = visibleNav('Admin');

    expect(groups).toHaveLength(3);
    expect(groups.flatMap((g) => g.items)).toHaveLength(10);
  });

  it('N4: Customer and signed-out see nothing', () => {
    expect(visibleNav('Customer')).toEqual([]);
    expect(visibleNav(null)).toEqual([]);
  });
});
