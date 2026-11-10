import { useMutation, useQueryClient } from '@tanstack/react-query';

import { queryKeys } from '@/lib/query-keys';

import { patchInventoryPage } from '../adjust-model';
import { adjustStock } from '../api';
import type { AdminInventoryPage } from '../types';

export function useAdjustStockMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: adjustStock,
    // Owner rule: patch the one row from the 200 body. No invalidate and no refetch, on success or on settle.
    onSuccess: (item) => {
      queryClient.setQueriesData<AdminInventoryPage>({ queryKey: queryKeys.admin.inventory.all }, (page) => patchInventoryPage(page, item));
    },
  });
}
