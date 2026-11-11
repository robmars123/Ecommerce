import { useQuery } from '@tanstack/react-query';

import { queryKeys } from '@/lib/query-keys';

import { fetchAdminCustomers } from '../api';

/** No placeholderData: kept data would hide an error and a page past the end (slice 12.2 D4). */
export function useAdminCustomersQuery(search: string | undefined, page: number) {
  return useQuery({
    queryKey: queryKeys.admin.customers.list(search, page),
    queryFn: ({ signal }) => fetchAdminCustomers(search, page, signal),
  });
}
