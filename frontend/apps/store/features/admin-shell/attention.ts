import type { Dashboard } from '@/features/admin-dashboard/types';
import type { UserRole } from '@/features/auth/types';

export type AttentionItem = { text: string; href?: string; tone?: 'warning' | 'danger' };

export type AttentionState = { kind: 'ready'; items: AttentionItem[] } | { kind: 'unavailable' };

const plural = (count: number, one: string, many: string) => `${count} ${count === 1 ? one : many}`;

/** The non-zero things that need an admin's eye. Display only: low stock and returns are Admin's to act on; the server returns every count to both roles. */
export function attentionItems(dashboard: Dashboard, role: UserRole | null): AttentionItem[] {
  const isAdmin = role === 'Admin';
  const { pendingReturns, deadLetters, laggingProjectors } = dashboard.needsAttention;
  const failed = deadLetters.reduce((sum, d) => sum + d.count, 0);
  const lag = laggingProjectors.reduce((max, p) => Math.max(max, p.lagSeconds), 0);
  const items: (AttentionItem | null)[] = [
    isAdmin && dashboard.lowStockCount > 0
      ? { text: plural(dashboard.lowStockCount, 'low-stock variant', 'low-stock variants'), href: '/admin/inventory', tone: 'warning' }
      : null,
    dashboard.cancelledCount > 0 ? { text: `${dashboard.cancelledCount} cancelled today` } : null,
    isAdmin && pendingReturns > 0
      ? { text: `${plural(pendingReturns, 'return request', 'return requests')} waiting`, href: '/admin/returns' }
      : null,
    failed > 0 ? { text: plural(failed, 'failed event', 'failed events'), tone: 'danger' } : null,
    lag > 0 ? { text: `Data ${lag} s behind`, tone: 'warning' } : null,
  ];
  return items.filter((item): item is AttentionItem => item !== null);
}
