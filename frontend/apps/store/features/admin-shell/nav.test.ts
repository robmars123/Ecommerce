import { ADMIN_NAV, visibleNav } from './nav';

const labels = (groups: { items: { label: string }[] }[]) => groups.map((g) => g.items.map((i) => i.label));

describe('admin nav', () => {
  it('N1: the table has the planned groups, order, labels, and hrefs', () => {
    expect(ADMIN_NAV).toEqual([
      {
        title: 'Overview',
        items: [
          { label: 'Dashboard', href: '/admin' },
          { label: 'User Stories', href: '/admin?tab=stories' },
        ],
      },
      {
        title: 'Manage',
        items: [
          { label: 'Orders', href: '/admin/orders' },
          { label: 'Products', href: '/admin/products' },
          { label: 'Categories', href: '/admin/categories' },
          { label: 'Inventory', href: '/admin/inventory' },
          { label: 'Returns', href: '/admin/returns' },
          { label: 'Customers', href: '/admin/customers' },
        ],
      },
      {
        title: 'Insights',
        items: [
          { label: 'Analytics', href: '/admin/analytics' },
          { label: 'Settings', href: '/admin/settings' },
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
