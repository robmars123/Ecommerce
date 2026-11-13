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
  UploadedImage,
} from './types';

const BASE = '/api/v1/admin/products';
const CATEGORIES = '/api/v1/admin/categories';
/** A 5 MB image on a slow uplink can pass the 15 s default. */
const UPLOAD_TIMEOUT_MS = 60_000;

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

/** Multipart upload of one image (field `file`). Stores the bytes only; the product keeps the key after its next save. */
export function uploadProductImage(id: string, form: FormData): Promise<UploadedImage> {
  return apiSend<UploadedImage>('POST', `${BASE}/${encodeURIComponent(id)}/images`, { body: form, timeoutMs: UPLOAD_TIMEOUT_MS });
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
