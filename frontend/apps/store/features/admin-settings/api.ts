import { apiGet } from '@/lib/api-client';

import type { AdminSettings } from './types';

export function fetchAdminSettings(signal?: AbortSignal): Promise<AdminSettings> {
  return apiGet<AdminSettings>('/api/v1/admin/settings', { signal });
}
