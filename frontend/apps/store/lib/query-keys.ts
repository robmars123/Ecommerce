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
  },
  auth: {
    me: ['auth', 'me'] as const,
  },
};
