import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { queryKeys } from '@/lib/query-keys';

import { fetchProducts } from '../api';
import type { ProductListParams } from '../types';

export function useProductsQuery(params: ProductListParams) {
  return useQuery({
    queryKey: queryKeys.products.list(params),
    queryFn: ({ signal }) => fetchProducts(params, signal),
    placeholderData: keepPreviousData,
  });
}
