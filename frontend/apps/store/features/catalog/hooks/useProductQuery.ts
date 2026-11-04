import { useQuery } from '@tanstack/react-query';

import { queryKeys } from '@/lib/query-keys';

import { fetchProduct } from '../api';

export function useProductQuery(slug: string) {
  return useQuery({
    queryKey: queryKeys.products.detail(slug),
    queryFn: ({ signal }) => fetchProduct(slug, signal),
  });
}
