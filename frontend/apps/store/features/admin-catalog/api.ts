import { apiGet, apiSend } from '@/lib/api-client';
import type { Category } from '@/features/catalog/types';

import type {
  AdminPreview,
  AdminProduct,
  AdminProductListPage,
  AdminProductListParams,
  CreateCategoryRequest,
  CreateProductRequest,
  UpdateCategoryRequest,
  UpdateProductRequest,
} from './types';

const BASE = '/api/v1/admin/products';
const CATEGORIES = '/api/v1/admin/categories';

export function fetchAdminProducts(params: AdminProductListParams, signal?: AbortSignal): Promise<AdminProductListPage> {
  return apiGet<AdminProductListPage>(BASE, {
    params: {
      search: params.search,
      category: params.category,
      status: params.status,
      page: params.page,
      pageSize: params.pageSize,
    },
    signal,
  });
}

export function fetchAdminProduct(id: string, signal?: AbortSignal): Promise<AdminProduct> {
  return apiGet<AdminProduct>(`${BASE}/${encodeURIComponent(id)}`, { signal });
}

export function fetchAdminPreview(id: string, signal?: AbortSignal): Promise<AdminPreview> {
  return apiGet<AdminPreview>(`${BASE}/${encodeURIComponent(id)}/preview`, { signal });
}

export function createProduct(body: CreateProductRequest): Promise<AdminProduct> {
  return apiSend<AdminProduct>('POST', BASE, { body });
}

export function updateProduct(id: string, body: UpdateProductRequest): Promise<AdminProduct> {
  return apiSend<AdminProduct>('PUT', `${BASE}/${encodeURIComponent(id)}`, { body });
}

export function publishProduct(id: string, publish: boolean): Promise<AdminProduct> {
  return apiSend<AdminProduct>('POST', `${BASE}/${encodeURIComponent(id)}/${publish ? 'publish' : 'unpublish'}`);
}

export function createCategory(body: CreateCategoryRequest): Promise<Category> {
  return apiSend<Category>('POST', CATEGORIES, { body });
}

export function updateCategory(currentSlug: string, body: UpdateCategoryRequest): Promise<Category> {
  return apiSend<Category>('PUT', `${CATEGORIES}/${encodeURIComponent(currentSlug)}`, { body });
}

export function deleteCategory(slug: string): Promise<void> {
  return apiSend<void>('DELETE', `${CATEGORIES}/${encodeURIComponent(slug)}`);
}
