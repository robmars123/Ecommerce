import { useMutation, useQueryClient } from '@tanstack/react-query';

import { queryKeys } from '@/lib/query-keys';

import { publishProduct } from '../api';

/** publish = true publishes, false unpublishes. */
export function usePublishProductMutation(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (publish: boolean) => publishProduct(id, publish),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.admin.products.all }),
        queryClient.invalidateQueries({ queryKey: queryKeys.products.all }),
      ]);
    },
  });
}
