import { useQuery } from '@tanstack/react-query';

import { queryKeys } from '@/lib/query-keys';

import { fetchAdminCustomer } from '../api';

export function useAdminCustomerQuery(id: string) {
  return useQuery({ queryKey: queryKeys.admin.customers.detail(id), queryFn: ({ signal }) => fetchAdminCustomer(id, signal) });
}
