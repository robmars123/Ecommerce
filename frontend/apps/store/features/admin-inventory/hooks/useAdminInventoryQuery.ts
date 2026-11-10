import { useQuery } from '@tanstack/react-query';

import { queryKeys } from '@/lib/query-keys';

import { fetchAdminInventory } from '../api';

export function useAdminInventoryQuery(page: number, pageSize: number) {
  return useQuery({
    queryKey: queryKeys.admin.inventory.list(page, pageSize),
    queryFn: ({ signal }) => fetchAdminInventory(page, pageSize, signal),
  });
}
