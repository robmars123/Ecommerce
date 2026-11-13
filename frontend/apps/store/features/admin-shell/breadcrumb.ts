import { ADMIN_NAV } from '@/features/admin-shell/nav';

export type Crumb = { label: string; href: string };

const DETAIL_LABELS: Record<string, string> = {
  orders: 'Order details',
  products: 'Product details',
  customers: 'Customer details',
};

function navCrumb(href: string): Crumb | null {
  const item = ADMIN_NAV.flatMap((group) => group.items).find((i) => i.href === href);
  return item ? { label: item.label, href: item.href } : null;
}

/** Trail for an admin path. Labels come only from the nav or a fixed table, never from the URL; unknown paths give []. */
export function breadcrumbFor(pathname: string): Crumb[] {
  const path = pathname.length > 1 && pathname.endsWith('/') ? pathname.slice(0, -1) : pathname;
  const segments = path.split('/').slice(1);
  if (segments[0] !== 'admin' || segments.some((s) => s === '')) return [];

  const root = navCrumb('/admin');
  if (!root) return [];
  if (segments.length === 1) return [root];

  const section = navCrumb(`/admin/${segments[1]}`);
  if (!section) return [];
  if (segments.length === 2) return [root, section];
  if (segments.length > 3) return [];

  const detail = segments[1] === 'products' && segments[2] === 'new' ? 'New product' : DETAIL_LABELS[segments[1]];
  if (detail === undefined) return [];
  return [root, section, { label: detail, href: path }];
}
