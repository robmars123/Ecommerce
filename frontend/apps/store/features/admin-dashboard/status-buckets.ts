import type { OrderStatus } from '@/features/orders/types';

export type StatusTone = 'warning' | 'muted' | 'success' | 'danger';

/** Display groups only (D11); the API keeps all 8 statuses. Typed so every status is placed exactly once. */
export const STATUS_BUCKETS: readonly { key: string; label: string; tone: StatusTone; statuses: readonly OrderStatus[] }[] = [
  { key: 'awaiting', label: 'Awaiting payment', tone: 'warning', statuses: ['PendingPayment'] },
  { key: 'in-progress', label: 'In progress', tone: 'muted', statuses: ['Paid', 'Processing', 'Shipped'] },
  { key: 'completed', label: 'Completed', tone: 'success', statuses: ['Delivered'] },
  { key: 'closed', label: 'Cancelled or refunded', tone: 'danger', statuses: ['Cancelled', 'Refunded', 'Returned'] },
];

export type StatusBucket = {
  key: string;
  label: string;
  tone: StatusTone;
  count: number;
  /** Display only: rounded, so the four may not add up to 100. */
  percent: number;
  /** The member statuses with a count above zero. */
  statuses: { status: string; count: number }[];
  /** The orders-list link, only for a single-status bucket (the order filter takes one status, D12). */
  href?: { pathname: '/admin/orders'; params: { status: string } };
};

export function statusBuckets(ordersByStatus: { status: string; count: number }[]): { total: number; buckets: StatusBucket[] } {
  const counts = new Map(ordersByStatus.map((s) => [s.status, s.count]));
  const grouped = STATUS_BUCKETS.map((b) => {
    const members = b.statuses.map((status) => ({ status: status as string, count: counts.get(status) ?? 0 }));
    return { b, members, count: members.reduce((sum, m) => sum + m.count, 0) };
  });
  const total = grouped.reduce((sum, x) => sum + x.count, 0);
  return {
    total,
    buckets: grouped.map(({ b, members, count }) => ({
      key: b.key,
      label: b.label,
      tone: b.tone,
      count,
      percent: total === 0 ? 0 : Math.round((count / total) * 100),
      statuses: members.filter((m) => m.count > 0),
      ...(b.statuses.length === 1 ? { href: { pathname: '/admin/orders' as const, params: { status: b.statuses[0] as string } } } : {}),
    })),
  };
}
