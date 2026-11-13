import { makeDashboard } from '@/tests/admin-dashboard-fixtures';

import { navBadges } from './badges';

describe('navBadges', () => {
  it('B1: a low-stock count puts "N low" on Inventory', () => {
    expect(navBadges(makeDashboard({ lowStockCount: 3, ordersByStatus: [] }))).toEqual({ '/admin/inventory': { text: '3 low' } });
  });

  it('B2: zero gives no badge', () => {
    expect(navBadges(makeDashboard({ lowStockCount: 0, ordersByStatus: [] }))).toEqual({});
  });

  it('B3: uses the count, not the length of the capped list', () => {
    expect(navBadges(makeDashboard({ lowStockCount: 35, ordersByStatus: [] }))).toEqual({ '/admin/inventory': { text: '35 low' } });
  });

  it('J4: the Paid count puts "N paid" on Orders, linking to the Paid list', () => {
    expect(navBadges(makeDashboard({ lowStockCount: 0, ordersByStatus: [{ status: 'Paid', count: 4 }] }))).toEqual({
      '/admin/orders': { text: '4 paid', href: '/admin/orders?status=Paid' },
    });
  });

  it('J4: no Paid entry or a zero count gives no Orders badge', () => {
    expect(navBadges(makeDashboard({ lowStockCount: 0, ordersByStatus: [{ status: 'Shipped', count: 2 }] }))).toEqual({});
    expect(navBadges(makeDashboard({ lowStockCount: 0, ordersByStatus: [{ status: 'Paid', count: 0 }] }))).toEqual({});
  });
});
