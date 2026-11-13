import { adminRouteDecision } from '@/features/auth/admin-access';
import type { UserRole } from '@/features/auth/types';

export type NavItem = { label: string; href: string; ready: boolean };
export type NavGroup = { title: string; items: NavItem[] };

export const ADMIN_NAV: readonly NavGroup[] = [
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
      { label: 'Analytics', href: '/admin/analytics', ready: true },
      { label: 'Settings', href: '/admin/settings', ready: true },
    ],
  },
];

/** UI only: hides what the route gate would redirect anyway. The API enforces roles on every request (arch §3.4 rule 11). */
export function visibleNav(role: UserRole | null): NavGroup[] {
  return ADMIN_NAV.map((group) => ({
    title: group.title,
    items: group.items.filter((item) => adminRouteDecision(role, item.href) === null),
  })).filter((group) => group.items.length > 0);
}
