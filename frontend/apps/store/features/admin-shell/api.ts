import { apiGet } from '@/lib/api-client';

import type { AdminSearchResult } from './types';

export function fetchAdminSearch(q: string, signal?: AbortSignal): Promise<AdminSearchResult> {
  return apiGet<AdminSearchResult>('/api/v1/admin/search', { params: { q }, signal });
}
