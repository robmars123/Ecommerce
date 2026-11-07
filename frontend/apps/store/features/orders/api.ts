import { apiGet } from '@/lib/api-client';

import type { OrderDetail, OrderListPage } from './types';

export const ORDER_PAGE_SIZE = 10;

export function fetchOrders(page: number, signal?: AbortSignal): Promise<OrderListPage> {
  return apiGet<OrderListPage>('/api/v1/orders', { params: { page, pageSize: ORDER_PAGE_SIZE }, signal });
}

export function fetchOrder(id: string, signal?: AbortSignal): Promise<OrderDetail> {
  return apiGet<OrderDetail>(`/api/v1/orders/${encodeURIComponent(id)}`, { signal });
}
