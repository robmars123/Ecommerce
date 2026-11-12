import type { AdminOrderFilters } from '@/features/admin-orders/types';
import type { AdminProductListParams } from '@/features/admin-catalog/types';
import type { ProductListParams } from '@/features/catalog/types';

/** Drops undefined values and fixes key order so equal filters always produce equal keys. */
function normalize(p: ProductListParams) {
  return {
    ...(p.search ? { search: p.search } : {}),
    ...(p.category ? { category: p.category } : {}),
    ...(p.sort ? { sort: p.sort } : {}),
    page: p.page,
    pageSize: p.pageSize,
  };
}

function normalizeAdmin(p: AdminProductListParams) {
  return {
    ...(p.search ? { search: p.search } : {}),
    ...(p.category ? { category: p.category } : {}),
    ...(p.status && p.status !== 'all' ? { status: p.status } : {}),
    page: p.page,
    pageSize: p.pageSize,
  };
}

function normalizeAdminOrders(f: AdminOrderFilters) {
  return {
    ...(f.status !== 'all' ? { status: f.status } : {}),
    ...(f.from.trim() ? { from: f.from.trim() } : {}),
    ...(f.to.trim() ? { to: f.to.trim() } : {}),
    ...(f.customer.trim() ? { customer: f.customer.trim() } : {}),
    ...(f.orderNumber.trim() ? { orderNumber: f.orderNumber.trim() } : {}),
  };
}

export const queryKeys = {
  products: {
    all: ['products'] as const,
    list: (p: ProductListParams) => ['products', 'list', normalize(p)] as const,
    detail: (slug: string) => ['products', 'detail', slug] as const,
  },
  categories: {
    all: ['categories'] as const,
  },
  admin: {
    products: {
      all: ['admin', 'products'] as const,
      list: (p: AdminProductListParams) => ['admin', 'products', 'list', normalizeAdmin(p)] as const,
      detail: (id: string) => ['admin', 'products', 'detail', id] as const,
      preview: (id: string) => ['admin', 'products', 'preview', id] as const,
    },
    inventory: {
      all: ['admin', 'inventory'] as const,
      list: (page: number, pageSize: number) => ['admin', 'inventory', 'list', page, pageSize] as const,
    },
    orders: {
      all: ['admin', 'orders'] as const,
      list: (f: AdminOrderFilters, page: number) => ['admin', 'orders', 'list', normalizeAdminOrders(f), page] as const,
      detail: (id: string) => ['admin', 'orders', 'detail', id] as const,
    },
    customers: {
      all: ['admin', 'customers'] as const,
      list: (search: string | undefined, page: number) => ['admin', 'customers', 'list', { ...(search ? { search } : {}), page }] as const,
      detail: (id: string) => ['admin', 'customers', 'detail', id] as const,
    },
    search: (q: string) => ['admin', 'search', q] as const,
    dashboard: (period: string) => ['admin', 'dashboard', period] as const,
    recentOrders: (page: number) => ['admin', 'dashboard', 'recent-orders', page] as const,
    returns: {
      all: ['admin', 'returns'] as const,
      list: (status: string, page: number) => ['admin', 'returns', 'list', status, page] as const,
    },
  },
  orders: {
    all: ['orders'] as const,
    list: (page: number) => ['orders', 'list', page] as const,
    detail: (id: string) => ['orders', 'detail', id] as const,
  },
  cart: ['cart'] as const,
  shippingMethods: ['shipping-methods'] as const,
  auth: {
    me: ['auth', 'me'] as const,
  },
};
