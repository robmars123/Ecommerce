import { useQuery } from '@tanstack/react-query';

import { queryKeys } from '@/lib/query-keys';

import { fetchOrder } from '../api';

export const ORDER_POLL_MS = 2000;

/**
 * One order. With `poll` it refetches every 2 seconds until the order leaves PendingPayment (Paid or Cancelled): the
 * checkout screen waits for the server, the only proof of payment, and never trusts the payment sheet (F5).
 */
export function useOrderQuery(id: string, poll = false) {
  return useQuery({
    queryKey: queryKeys.orders.detail(id),
    queryFn: ({ signal }) => fetchOrder(id, signal),
    refetchInterval: poll ? (query) => (query.state.data && query.state.data.status !== 'PendingPayment' ? false : ORDER_POLL_MS) : false,
  });
}
