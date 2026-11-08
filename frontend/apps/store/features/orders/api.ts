import { apiGet, apiSend } from '@/lib/api-client';

import type { OrderDetail, OrderListPage, ReturnRequestBody } from './types';

export const ORDER_PAGE_SIZE = 10;

export function fetchOrders(page: number, signal?: AbortSignal): Promise<OrderListPage> {
  return apiGet<OrderListPage>('/api/v1/orders', { params: { page, pageSize: ORDER_PAGE_SIZE }, signal });
}

export function fetchOrder(id: string, signal?: AbortSignal): Promise<OrderDetail> {
  return apiGet<OrderDetail>(`/api/v1/orders/${encodeURIComponent(id)}`, { signal });
}

/**
 * No Idempotency-Key (arch 9): the server allows one open request per order, so a double submit is a 409, never a second
 * request. The screen waits for the answer (F8).
 */
export function requestReturn(orderId: string, body: ReturnRequestBody): Promise<{ returnId: string }> {
  return apiSend<{ returnId: string }>('POST', `/api/v1/orders/${encodeURIComponent(orderId)}/return-request`, { body });
}
