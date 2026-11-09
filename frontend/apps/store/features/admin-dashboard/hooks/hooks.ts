import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { queryKeys } from '@/lib/query-keys';

import { fetchDashboard, fetchRecentOrders } from '../api';
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
