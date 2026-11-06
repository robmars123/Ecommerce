import { useQuery } from '@tanstack/react-query';

import { queryKeys } from '@/lib/query-keys';

import { fetchAdminProduct } from '../api';

export function useAdminProductQuery(id: string) {
  return useQuery({
    queryKey: queryKeys.admin.products.detail(id),
    queryFn: ({ signal }) => fetchAdminProduct(id, signal),
  });
}
