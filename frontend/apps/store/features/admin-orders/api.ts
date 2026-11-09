import { apiGet, apiSend } from '@/lib/api-client';

import type { AdminOrderAction, AdminOrderDetail, AdminOrderFilters, AdminOrderListPage, RefundAccepted, RefundBody, ReturnListPage, ReturnStatus } from './types';

export const RETURN_PAGE_SIZE = 20;
export const ADMIN_ORDER_PAGE_SIZE = 20;

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

/** Empty fields and 'all' are left out; values are trimmed (arch 13.2). */
export function fetchAdminOrders(filters: AdminOrderFilters, page: number, signal?: AbortSignal): Promise<AdminOrderListPage> {
  const text = (value: string) => (value.trim().length > 0 ? value.trim() : undefined);
  return apiGet<AdminOrderListPage>('/api/v1/admin/orders', {
    params: {
      status: filters.status === 'all' ? undefined : filters.status,
      from: text(filters.from),
      to: text(filters.to),
      customer: text(filters.customer),
      orderNumber: text(filters.orderNumber),
      page,
      pageSize: ADMIN_ORDER_PAGE_SIZE,
    },
    signal,
  });
}

/** No Idempotency-Key: the status transition is the guard, a repeat answers 409 (13.2). */
export function runOrderAction(orderId: string, action: AdminOrderAction): Promise<void> {
  const base = `/api/v1/admin/orders/${encodeURIComponent(orderId)}`;
  switch (action.kind) {
    case 'process':
      return apiSend<void>('POST', `${base}/process`, {});
    case 'deliver':
      return apiSend<void>('POST', `${base}/deliver`, {});
    case 'ship':
      return apiSend<void>('POST', `${base}/ship`, { body: { carrierCode: action.carrierCode, trackingNumber: action.trackingNumber } });
    case 'override':
      return apiSend<void>('POST', `${base}/status-override`, { body: { toStatus: action.toStatus, reason: action.reason } });
  }
}
