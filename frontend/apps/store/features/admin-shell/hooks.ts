import { useQuery } from '@tanstack/react-query';

import { fetchDashboard } from '@/features/admin-dashboard/api';
import { adminRouteDecision } from '@/features/auth/admin-access';
import type { UserRole } from '@/features/auth/types';
import { queryKeys } from '@/lib/query-keys';

/** Matches the projection lag target (arch 8.8). */
export const SHELL_REFRESH_MS = 60_000;

/** The shell's figures come from the Today dashboard (same key as the dashboard screen, so one request on /admin). */
export function useShellDashboardQuery(role: UserRole | null) {
  return useQuery({
    queryKey: queryKeys.admin.dashboard('today'),
    queryFn: ({ signal }) => fetchDashboard('today', signal),
    enabled: adminRouteDecision(role, '/admin') === null,
    refetchInterval: SHELL_REFRESH_MS,
  });
}
