import { useMutation, useQueryClient } from '@tanstack/react-query';

import { queryKeys } from '@/lib/query-keys';

import { requestReturn } from '../api';
import type { ReturnRequestBody } from '../types';

/** No optimistic update (F8): the screen shows "requested" only after the server created the request. */
export function useReturnRequestMutation(orderId: string) {
  const client = useQueryClient();
  return useMutation<{ returnId: string }, Error, ReturnRequestBody>({
    mutationFn: (body) => requestReturn(orderId, body),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.orders.detail(orderId) }),
  });
}
