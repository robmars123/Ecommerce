import type { UserRole } from './types';

const SUPPORT_PREFIXES = ['/admin/orders', '/admin/customers'];

function isUnder(path: string, prefix: string): boolean {
  return path === prefix || path.startsWith(`${prefix}/`);
}

/**
 * Where an /admin page should send the visitor: null means "show it". This is only the UI hiding things; the API
 * enforces the same roles on every request.
 */
export function adminRouteDecision(role: UserRole | null, path: string): string | null {
  if (role === null) return `/login?returnTo=${encodeURIComponent(path)}`;
  if (role === 'Customer') return '/';
  if (role === 'Admin') return null;
  return path === '/admin' || SUPPORT_PREFIXES.some((p) => isUnder(path, p)) ? null : '/admin';
}
