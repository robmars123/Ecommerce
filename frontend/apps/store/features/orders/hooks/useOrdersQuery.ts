import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { queryKeys } from '@/lib/query-keys';

import { fetchOrders } from '../api';

export function useOrdersQuery(page: number) {
  return useQuery({
    queryKey: queryKeys.orders.list(page),
    queryFn: ({ signal }) => fetchOrders(page, signal),
    placeholderData: keepPreviousData,
  });
}
