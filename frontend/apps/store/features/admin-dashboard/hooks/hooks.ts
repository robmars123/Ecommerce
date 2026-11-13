import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { queryKeys } from '@/lib/query-keys';

import { fetchDashboard, fetchRecentOrders, fetchRevenueTrend } from '../api';
import type { DashboardPeriod } from '../types';

export function useDashboardQuery(period: DashboardPeriod) {
  return useQuery({
    queryKey: queryKeys.admin.dashboard(period),
    queryFn: ({ signal }) => fetchDashboard(period, signal),
    placeholderData: keepPreviousData,
  });
}

export function useRecentOrdersQuery(page: number) {
  return useQuery({
    queryKey: queryKeys.admin.recentOrders(page),
    queryFn: ({ signal }) => fetchRecentOrders(page, signal),
    placeholderData: keepPreviousData,
  });
}

/** Admin only: `enabled` is the role gate, so Support never requests revenue (the API 403 is the real guard). */
export function useRevenueTrendQuery(period: DashboardPeriod, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.admin.trend(period),
    queryFn: ({ signal }) => fetchRevenueTrend(period, signal),
    placeholderData: keepPreviousData,
    enabled,
  });
}
