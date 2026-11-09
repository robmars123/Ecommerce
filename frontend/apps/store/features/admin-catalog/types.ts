// Wire types for the admin catalog API (architecture 9, Slice 1b). Money is a string typed by the admin and
// stored exactly by the server; the UI never does arithmetic on it.
import type { CategoryRef, Money, ProductDetail } from '@/features/catalog/types';

export type AdminStatus = 'all' | 'active' | 'inactive';

export type AdminProductListParams = {
  search?: string;
  category?: string;
  status?: AdminStatus;
  page: number;
  pageSize: number;
};

export type AdminProductListItem = {
  id: string;
  slug: string;
  name: string;
  category: CategoryRef;
  isActive: boolean;
  variantCount: number;
  activeVariantCount: number;
  price: Money | null;
  updatedAt: string;
};

export type AdminProductListPage = {
  items: AdminProductListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type AdminVariant = {
  id: string;
  sku: string;
  name: string;
  size: string | null;
  colour: string | null;
  price: Money;
  compareAtPrice: Money | null;
  weightGrams: number;
  isActive: boolean;
};

export type AdminImage = { storageKey: string; altText: string; sortOrder: number; url: string | null };

export type AdminProduct = {
  id: string;
  slug: string;
  name: string;
  description: string;
  category: CategoryRef;
  isActive: boolean;
  version: number;
  createdAt: string;
  updatedAt: string;
  variants: AdminVariant[];
  images: AdminImage[];
};

export type AdminPreview = { product: ProductDetail; isActive: boolean; isVisibleToCustomers: boolean };

export type VariantRequest = {
  id?: string;
  sku: string;
  name: string;
  size: string | null;
  colour: string | null;
  price: Money;
  compareAtPrice: Money | null;
  weightGrams: number;
  /** Sent on update only: a create request with isActive is rejected by the server. */
  isActive?: boolean;
};

export type ImageRequest = { storageKey: string; altText: string };

export type CreateProductRequest = {
  slug: string;
  name: string;
  description: string;
  categorySlug: string;
  variants: VariantRequest[];
  images: ImageRequest[];
};

export type UpdateProductRequest = CreateProductRequest & { version: number };

export type CreateCategoryRequest = { slug: string; name: string; parentSlug: string | null };

/** Parent is fixed at create: the server answers 400 if a PUT carries parentSlug. */
export type UpdateCategoryRequest = { slug: string; name: string };
