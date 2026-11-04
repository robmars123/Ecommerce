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

export const queryKeys = {
  products: {
    all: ['products'] as const,
    list: (p: ProductListParams) => ['products', 'list', normalize(p)] as const,
    detail: (slug: string) => ['products', 'detail', slug] as const,
  },
  categories: {
    all: ['categories'] as const,
  },
};
