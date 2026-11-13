import { makeDashboard } from '@/tests/admin-dashboard-fixtures';

import { navBadges } from './badges';

describe('navBadges', () => {
  it('B1: a low-stock count puts "N low" on Inventory', () => {
    expect(navBadges(makeDashboard({ lowStockCount: 3 }))).toEqual({ '/admin/inventory': '3 low' });
  });

  it('B2: zero gives no badge', () => {
    expect(navBadges(makeDashboard({ lowStockCount: 0 }))).toEqual({});
  });

  it('B3: uses the count, not the length of the capped list', () => {
    expect(navBadges(makeDashboard({ lowStockCount: 35 }))).toEqual({ '/admin/inventory': '35 low' });
  });
});
