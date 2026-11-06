import { adminRouteDecision } from './admin-access';
import type { UserRole } from './types';

const PATHS = ['/admin', '/admin/products', '/admin/products/7', '/admin/inventory', '/admin/orders/9', '/admin/returns', '/admin/customers/3'];

// null = show the page; a string = redirect there.
const EXPECTED: Record<string, Array<string | null>> = {
  none: PATHS.map((p) => `/login?returnTo=${encodeURIComponent(p)}`),
  Customer: PATHS.map(() => '/'),
  Support: [null, '/admin', '/admin', '/admin', null, '/admin', null],
  Admin: PATHS.map(() => null),
};

describe('adminRouteDecision', () => {
  it.each(Object.keys(EXPECTED))('%s', (who) => {
    const role = who === 'none' ? null : (who as UserRole);
    expect(PATHS.map((p) => adminRouteDecision(role, p))).toEqual(EXPECTED[who]);
  });

  it('does not treat a longer sibling name as an allowed Support prefix', () => {
    expect(adminRouteDecision('Support', '/admin/orders-export')).toBe('/admin');
  });
});
