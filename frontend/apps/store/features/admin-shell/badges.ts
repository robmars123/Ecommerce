import type { Dashboard } from '@/features/admin-dashboard/types';

/** Small counts shown beside sidebar items, keyed by nav href. Display only. */
export type NavBadges = Readonly<Partial<Record<string, string>>>;

export function navBadges(dashboard: Dashboard): NavBadges {
  return dashboard.lowStockCount > 0 ? { '/admin/inventory': `${dashboard.lowStockCount} low` } : {};
}
