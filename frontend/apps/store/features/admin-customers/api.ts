import { apiGet } from '@/lib/api-client';

import type { AdminCustomerDetail, AdminCustomerListPage } from './types';

export const ADMIN_CUSTOMER_PAGE_SIZE = 20;

export function fetchAdminCustomers(search: string | undefined, page: number, signal?: AbortSignal): Promise<AdminCustomerListPage> {
  return apiGet<AdminCustomerListPage>('/api/v1/admin/customers', {
    params: { search: search || undefined, page, pageSize: ADMIN_CUSTOMER_PAGE_SIZE },
    signal,
  });
}

export function fetchAdminCustomer(id: string, signal?: AbortSignal): Promise<AdminCustomerDetail> {
  return apiGet<AdminCustomerDetail>(`/api/v1/admin/customers/${encodeURIComponent(id)}`, { signal });
}
