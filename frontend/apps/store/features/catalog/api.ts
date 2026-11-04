import { apiGet } from '@/lib/api-client';

import type { CategoryList, ProductDetail, ProductListPage, ProductListParams } from './types';

export function fetchProducts(params: ProductListParams, signal?: AbortSignal): Promise<ProductListPage> {
  return apiGet<ProductListPage>('/api/v1/products', {
    params: {
      search: params.search,
      category: params.category,
      sort: params.sort,
      page: params.page,
      pageSize: params.pageSize,
    },
    signal,
  });
}

export function fetchProduct(slug: string, signal?: AbortSignal): Promise<ProductDetail> {
  return apiGet<ProductDetail>(`/api/v1/products/${encodeURIComponent(slug)}`, { signal });
}

export function fetchCategories(signal?: AbortSignal): Promise<CategoryList> {
  return apiGet<CategoryList>('/api/v1/categories', { signal });
}
