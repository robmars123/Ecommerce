import { apiGet } from '@/lib/api-client';

import type { Dashboard, DashboardPeriod, RecentOrdersPage } from './types';

export const RECENT_ORDERS_PAGE_SIZE = 10;

export function fetchDashboard(period: DashboardPeriod, signal?: AbortSignal): Promise<Dashboard> {
  return apiGet<Dashboard>('/api/v1/admin/dashboard', { params: { period }, signal });
}

export function fetchRecentOrders(page: number, signal?: AbortSignal): Promise<RecentOrdersPage> {
  return apiGet<RecentOrdersPage>('/api/v1/admin/dashboard/recent-orders', {
    params: { page, pageSize: RECENT_ORDERS_PAGE_SIZE },
    signal,
  });
}
