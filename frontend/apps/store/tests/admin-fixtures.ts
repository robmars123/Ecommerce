import type { AdminProduct, AdminProductListItem, AdminProductListPage } from '@/features/admin-catalog/types';

export function makeAdminProduct(overrides: Partial<AdminProduct> = {}): AdminProduct {
  return {
    id: 'a-1',
    slug: 'linen-shirt',
    name: 'Linen shirt',
    description: 'A shirt.',
    category: { slug: 'shirts', name: 'Shirts' },
    isActive: false,
    version: 100,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    variants: [
      {
        id: 'av-1',
        sku: 'LS-1',
        name: 'Default',
        size: null,
        colour: null,
        price: { amount: '49.00', currency: 'USD' },
        compareAtPrice: null,
        weightGrams: 250,
        isActive: true,
      },
    ],
    images: [],
    ...overrides,
  };
}

export function makeAdminItem(overrides: Partial<AdminProductListItem> = {}): AdminProductListItem {
  return {
    id: 'a-1',
    slug: 'linen-shirt',
    name: 'Linen shirt',
    category: { slug: 'shirts', name: 'Shirts' },
    isActive: false,
    variantCount: 1,
    activeVariantCount: 1,
    price: { amount: '49.00', currency: 'USD' },
    updatedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  };
}

export function makeAdminPage(items: AdminProductListItem[], overrides: Partial<AdminProductListPage> = {}): AdminProductListPage {
  return {
    items,
    page: 1,
    pageSize: 20,
    totalCount: items.length,
    totalPages: items.length === 0 ? 0 : 1,
    ...overrides,
  };
}
