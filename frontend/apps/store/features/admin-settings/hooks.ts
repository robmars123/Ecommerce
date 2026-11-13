import { useQuery } from '@tanstack/react-query';

import { queryKeys } from '@/lib/query-keys';

import { fetchAdminSettings } from './api';

/** `enabled` is the Admin gate: Support is redirected away and must not request it (the API 403 is the real guard). */
export function useAdminSettingsQuery(enabled: boolean) {
  return useQuery({ queryKey: queryKeys.admin.settings, queryFn: ({ signal }) => fetchAdminSettings(signal), enabled });
}
