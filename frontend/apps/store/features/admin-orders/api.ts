import { apiGet, apiSend } from '@/lib/api-client';

import type { AdminOrderDetail, RefundAccepted, RefundBody, ReturnListPage, ReturnStatus } from './types';

export const RETURN_PAGE_SIZE = 20;

export function fetchAdminOrder(id: string, signal?: AbortSignal): Promise<AdminOrderDetail> {
  return apiGet<AdminOrderDetail>(`/api/v1/admin/orders/${encodeURIComponent(id)}`, { signal });
}

/** The same key is sent again when this exact attempt is retried after a network failure (arch 8.5). */
export function submitRefund(orderId: string, body: RefundBody, idempotencyKey: string): Promise<RefundAccepted> {
  return apiSend<RefundAccepted>('POST', `/api/v1/admin/orders/${encodeURIComponent(orderId)}/refund`, {
    body,
    headers: { 'Idempotency-Key': idempotencyKey },
  });
}

export function fetchReturns(status: ReturnStatus | 'all', page: number, signal?: AbortSignal): Promise<ReturnListPage> {
  return apiGet<ReturnListPage>('/api/v1/admin/returns', {
    params: { status: status === 'all' ? undefined : status, page, pageSize: RETURN_PAGE_SIZE },
    signal,
  });
}

export function decideReturn(id: string, decision: 'approve' | 'decline', reason: string): Promise<void> {
  return apiSend<void>('POST', `/api/v1/admin/returns/${encodeURIComponent(id)}/${decision}`, { body: { reason } });
}
