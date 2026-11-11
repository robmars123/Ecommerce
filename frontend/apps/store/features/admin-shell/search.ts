import { adminRouteDecision } from '@/features/auth/admin-access';
import type { UserRole } from '@/features/auth/types';
import { statusLabel } from '@/features/orders/status-labels';

import type { AdminSearchResult } from './types';

export const ADMIN_SEARCH_DEBOUNCE_MS = 300;
const MIN_LENGTH = 2;
const MAX_LENGTH = 100;

export type SearchGroup = {
  title: 'Orders' | 'Products' | 'SKUs';
  items: { key: string; label: string; href: string }[];
};

/** The term to send, or null when it is too short (the API would answer 400). */
export function searchTerm(raw: string): string | null {
  const term = raw.trim().slice(0, MAX_LENGTH);
  return term.length < MIN_LENGTH ? null : term;
}

/** Groups to show: only those whose link target this role may open (UI hiding; the API enforces the roles). Server order is kept. */
export function searchGroups(role: UserRole | null, result: AdminSearchResult): SearchGroup[] {
  const canOrders = adminRouteDecision(role, '/admin/orders') === null;
  const canProducts = adminRouteDecision(role, '/admin/products') === null;
  const groups: SearchGroup[] = [];
  if (canOrders) {
    groups.push({
      title: 'Orders',
      items: result.orders.map((o) => ({
        key: o.id,
        label: `${o.orderNumber} · ${statusLabel(o.status)}`,
        href: `/admin/orders/${encodeURIComponent(o.id)}`,
      })),
    });
  }
  if (canProducts) {
    groups.push({
      title: 'Products',
      items: result.products.map((p) => ({ key: p.id, label: p.name, href: `/admin/products/${encodeURIComponent(p.id)}` })),
    });
    groups.push({
      title: 'SKUs',
      items: result.skus.map((s) => ({
        key: s.sku,
        label: `${s.sku} · ${s.productName}`,
        href: `/admin/products/${encodeURIComponent(s.productId)}`,
      })),
    });
  }
  return groups.filter((g) => g.items.length > 0);
}
