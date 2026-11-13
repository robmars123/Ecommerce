import { adminRouteDecision } from '@/features/auth/admin-access';
import type { UserRole } from '@/features/auth/types';

export type NavItem = { label: string; href: string };
export type NavGroup = { title: string; items: NavItem[] };

export const ADMIN_NAV: readonly NavGroup[] = [
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
];

/** UI only: hides what the route gate would redirect anyway. The API enforces roles on every request (arch §3.4 rule 11). */
export function visibleNav(role: UserRole | null): NavGroup[] {
  return ADMIN_NAV.map((group) => ({
    title: group.title,
    items: group.items.filter((item) => adminRouteDecision(role, item.href.split('?')[0]) === null),
  })).filter((group) => group.items.length > 0);
}
