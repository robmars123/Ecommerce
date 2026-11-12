import type { AdminSearchResult } from '@/features/admin-shell/types';

export function makeSearchResult(overrides: Partial<AdminSearchResult> = {}): AdminSearchResult {
  return {
    orders: [{ id: 'o1', orderNumber: '1001', status: 'Paid' }],
    products: [{ id: 'p1', name: 'Blue Mug' }],
    skus: [{ sku: 'MUG-BLU-L', productId: 'p1', productName: 'Blue Mug' }],
    ...overrides,
  };
}
