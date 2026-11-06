import { useMutation, useQueryClient } from '@tanstack/react-query';

import { queryKeys } from '@/lib/query-keys';

import { createProduct } from '../api';

/** No optimistic updates: the list refetches after the server confirms. */
export function useCreateProductMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createProduct,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.admin.products.all }),
        queryClient.invalidateQueries({ queryKey: queryKeys.products.all }),
      ]);
    },
  });
}
