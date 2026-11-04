import type {
  Category,
  ProductDetail,
  ProductListItem,
  ProductListPage,
  ProductVariant,
} from '@/features/catalog/types';

export function makeItem(overrides: Partial<ProductListItem> = {}): ProductListItem {
  return {
    id: 'p-1',
    slug: 'blue-shirt',
    name: 'Blue Shirt',
    category: { slug: 'tops', name: 'Tops' },
    price: { amount: '24.50', currency: 'USD' },
    compareAtPrice: null,
    primaryImage: { url: null, altText: 'Blue shirt, front' },
    stockStatus: 'Unknown',
    ...overrides,
  };
}

export function makePage(items: ProductListItem[], overrides: Partial<ProductListPage> = {}): ProductListPage {
  return {
    items,
    page: 1,
    pageSize: 20,
    totalCount: items.length,
    totalPages: items.length === 0 ? 0 : 1,
    ...overrides,
  };
}

export function makeVariant(overrides: Partial<ProductVariant> = {}): ProductVariant {
  return {
    id: 'v-1',
    sku: 'BS-M',
    name: 'Medium',
    size: 'M',
    colour: 'Blue',
    price: { amount: '24.50', currency: 'USD' },
    compareAtPrice: null,
    stockStatus: 'Unknown',
    ...overrides,
  };
}

export function makeDetail(overrides: Partial<ProductDetail> = {}): ProductDetail {
  return {
    id: 'p-1',
    slug: 'blue-shirt',
    name: 'Blue Shirt',
    description: 'A soft blue shirt.',
    category: { slug: 'tops', name: 'Tops' },
    images: [{ url: null, altText: 'Blue shirt, front' }],
    variants: [makeVariant()],
    ...overrides,
  };
}

export const categories: Category[] = [
  { slug: 'clothing', name: 'Clothing', parentSlug: null },
  { slug: 'tops', name: 'Tops', parentSlug: 'clothing' },
];
