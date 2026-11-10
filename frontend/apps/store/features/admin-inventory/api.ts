import { apiGet, apiSend } from '@/lib/api-client';

import type { AdjustStockRequest, AdminInventoryItem, AdminInventoryPage } from './types';

const BASE = '/api/v1/admin/inventory';

export function fetchAdminInventory(page: number, pageSize: number, signal?: AbortSignal): Promise<AdminInventoryPage> {
  return apiGet<AdminInventoryPage>(BASE, { params: { page, pageSize }, signal });
}

export function adjustStock(body: AdjustStockRequest): Promise<AdminInventoryItem> {
  return apiSend<AdminInventoryItem>('POST', `${BASE}/adjust`, { body });
}
