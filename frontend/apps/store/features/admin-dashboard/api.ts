import { apiGet, apiGetText } from '@/lib/api-client';

import type { Dashboard, DashboardPeriod, RecentOrdersPage, RevenueTrend } from './types';

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

export function fetchRevenueTrend(period: DashboardPeriod, signal?: AbortSignal): Promise<RevenueTrend> {
  return apiGet<RevenueTrend>('/api/v1/admin/dashboard/trend', { params: { period }, signal });
}

/** The trend as CSV text (Admin only). */
export function fetchDashboardExport(period: DashboardPeriod): Promise<string> {
  return apiGetText('/api/v1/admin/dashboard/export', { params: { period } });
}
