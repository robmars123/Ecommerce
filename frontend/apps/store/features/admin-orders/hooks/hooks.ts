import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { isDefinitiveFailure, useAttemptKey } from '@/features/checkout/hooks/useAttemptKey';
import { queryKeys } from '@/lib/query-keys';

import { decideReturn, fetchAdminOrder, fetchReturns, submitRefund } from '../api';
import type { RefundAccepted, RefundBody, ReturnStatus } from '../types';

export function useAdminOrderQuery(id: string) {
  return useQuery({ queryKey: queryKeys.admin.orders.detail(id), queryFn: ({ signal }) => fetchAdminOrder(id, signal) });
}

/**
 * One Idempotency-Key per refund attempt (F13, arch 8.5): kept across a retry after a network failure so the server returns
 * the same refund, renewed after a definite answer so the next attempt (a corrected amount) is a new request. No optimistic
 * status: the screen shows what the server returned, then refetches the order for the new Pending and remaining amounts.
 */
export function useRefundMutation(orderId: string) {
  const client = useQueryClient();
  const key = useAttemptKey();
  const mutation = useMutation<RefundAccepted, Error, RefundBody>({
    mutationFn: (body) => submitRefund(orderId, body, key.current()),
    onSuccess: () => {
      key.renew();
      return client.invalidateQueries({ queryKey: queryKeys.admin.orders.detail(orderId) });
    },
    onError: (error) => {
      if (isDefinitiveFailure(error)) {
        key.renew();
      }
    },
  });
  return { submit: mutation.mutate, isPending: mutation.isPending, error: mutation.error, data: mutation.data, reset: mutation.reset };
}

export function useReturnsQuery(status: ReturnStatus | 'all', page: number) {
  return useQuery({
    queryKey: queryKeys.admin.returns.list(status, page),
    queryFn: ({ signal }) => fetchReturns(status, page, signal),
    placeholderData: keepPreviousData,
  });
}

export function useDecideReturnMutation() {
  const client = useQueryClient();
  return useMutation<void, Error, { id: string; decision: 'approve' | 'decline'; reason: string }>({
    mutationFn: ({ id, decision, reason }) => decideReturn(id, decision, reason),
    onSettled: () => client.invalidateQueries({ queryKey: queryKeys.admin.returns.all }),
  });
}
