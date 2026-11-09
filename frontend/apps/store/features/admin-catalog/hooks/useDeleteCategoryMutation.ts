import { useMutation, useQueryClient } from '@tanstack/react-query';

import { queryKeys } from '@/lib/query-keys';

import { deleteCategory } from '../api';

export function useDeleteCategoryMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: deleteCategory,
    onSettled: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.categories.all });
    },
  });
}
