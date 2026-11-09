import { useMutation, useQueryClient } from '@tanstack/react-query';

import { queryKeys } from '@/lib/query-keys';

import { updateCategory } from '../api';
import type { UpdateCategoryRequest } from '../types';

export type UpdateCategoryInput = { currentSlug: string; body: UpdateCategoryRequest };

export function useUpdateCategoryMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ currentSlug, body }: UpdateCategoryInput) => updateCategory(currentSlug, body),
    // Settled, not just success: a 404 means someone else changed it, so refetch to show the truth.
    onSettled: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.categories.all }),
        queryClient.invalidateQueries({ queryKey: queryKeys.admin.products.all }),
        queryClient.invalidateQueries({ queryKey: queryKeys.products.all }),
      ]);
    },
  });
}
