import { useQuery } from '@tanstack/react-query';

import { queryKeys } from '@/lib/query-keys';

import { fetchShippingMethods } from '../api';

/** The configured flat rates (arch 7.4). The screen lists these; it never invents a method or a price. */
export function useShippingMethodsQuery() {
  return useQuery({
    queryKey: queryKeys.shippingMethods,
    queryFn: ({ signal }) => fetchShippingMethods(signal),
  });
}
