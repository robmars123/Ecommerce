import type { Dashboard } from '@/features/admin-dashboard/types';

/** A small count beside a sidebar item. With an href the badge is its own link. */
export type NavBadge = { text: string; href?: string };

/** Small counts shown beside sidebar items, keyed by nav href. Display only. */
export type NavBadges = Readonly<Partial<Record<string, NavBadge>>>;

export function navBadges(dashboard: Dashboard): NavBadges {
  const paid = dashboard.ordersByStatus.find((s) => s.status === 'Paid')?.count ?? 0;
  return {
    ...(dashboard.lowStockCount > 0 ? { '/admin/inventory': { text: `${dashboard.lowStockCount} low` } } : {}),
    ...(paid > 0 ? { '/admin/orders': { text: `${paid} paid`, href: '/admin/orders?status=Paid' } } : {}),
  };
}
