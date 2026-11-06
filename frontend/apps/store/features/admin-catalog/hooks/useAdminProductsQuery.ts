import { useQuery } from '@tanstack/react-query';

import { queryKeys } from '@/lib/query-keys';

import { fetchAdminProducts } from '../api';
import type { AdminProductListParams } from '../types';

export function useAdminProductsQuery(params: AdminProductListParams) {
  return useQuery({
    queryKey: queryKeys.admin.products.list(params),
    queryFn: ({ signal }) => fetchAdminProducts(params, signal),
  });
}
