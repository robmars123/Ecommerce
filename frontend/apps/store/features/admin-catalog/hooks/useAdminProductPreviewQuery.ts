import { useQuery } from '@tanstack/react-query';

import { queryKeys } from '@/lib/query-keys';

import { fetchAdminPreview } from '../api';

export function useAdminProductPreviewQuery(id: string, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.admin.products.preview(id),
    queryFn: ({ signal }) => fetchAdminPreview(id, signal),
    enabled,
  });
}
