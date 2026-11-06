import type { AdminProduct, CreateProductRequest, UpdateProductRequest, VariantRequest } from './types';

/** Everything the form edits is text, exactly as typed. Prices are never parsed or rounded here. */
export type VariantFormRow = {
  key: string;
  id?: string;
  sku: string;
  name: string;
  size: string;
  colour: string;
  price: string;
  compareAt: string;
  weightGrams: string;
  isActive: boolean;
};

export type ImageFormRow = { key: string; storageKey: string; altText: string };

export type ProductFormState = {
  slug: string;
  name: string;
  description: string;
  categorySlug: string;
  currency: string;
  variants: VariantFormRow[];
  images: ImageFormRow[];
};

let nextKey = 0;
export function newRowKey(): string {
  nextKey += 1;
  return `row-${nextKey}`;
}

export function emptyVariantRow(): VariantFormRow {
  return { key: newRowKey(), sku: '', name: '', size: '', colour: '', price: '', compareAt: '', weightGrams: '0', isActive: true };
}

export function emptyFormState(): ProductFormState {
  return { slug: '', name: '', description: '', categorySlug: '', currency: 'USD', variants: [emptyVariantRow()], images: [] };
}

export function toFormState(product: AdminProduct): ProductFormState {
  return {
    slug: product.slug,
    name: product.name,
    description: product.description,
    categorySlug: product.category.slug,
    currency: product.variants[0]?.price.currency ?? 'USD',
    variants: product.variants.map((v) => ({
      key: newRowKey(),
      id: v.id,
      sku: v.sku,
      name: v.name,
      size: v.size ?? '',
      colour: v.colour ?? '',
      price: v.price.amount,
      compareAt: v.compareAtPrice?.amount ?? '',
      weightGrams: String(v.weightGrams),
      isActive: v.isActive,
    })),
    images: product.images.map((i) => ({ key: newRowKey(), storageKey: i.storageKey, altText: i.altText })),
  };
}

function toVariant(row: VariantFormRow, currency: string, includeActive: boolean): VariantRequest {
  const weight = Number(row.weightGrams.trim());
  return {
    ...(row.id ? { id: row.id } : {}),
    sku: row.sku.trim(),
    name: row.name.trim(),
    size: row.size.trim() || null,
    colour: row.colour.trim() || null,
    price: { amount: row.price.trim(), currency },
    compareAtPrice: row.compareAt.trim() ? { amount: row.compareAt.trim(), currency } : null,
    weightGrams: Number.isInteger(weight) ? weight : 0,
    ...(includeActive ? { isActive: row.isActive } : {}),
  };
}

function common(state: ProductFormState, includeActive: boolean): CreateProductRequest {
  const currency = state.currency.trim().toUpperCase();
  return {
    slug: state.slug.trim(),
    name: state.name.trim(),
    description: state.description,
    categorySlug: state.categorySlug.trim(),
    variants: state.variants.map((v) => toVariant(v, currency, includeActive)),
    images: state.images.map((i) => ({ storageKey: i.storageKey.trim(), altText: i.altText.trim() })),
  };
}

/** A create request never carries isActive: new products start unpublished and the server rejects the field. */
export function toCreateRequest(state: ProductFormState): CreateProductRequest {
  return common(state, false);
}

export function toUpdateRequest(state: ProductFormState, version: number): UpdateProductRequest {
  return { ...common(state, true), version };
}
