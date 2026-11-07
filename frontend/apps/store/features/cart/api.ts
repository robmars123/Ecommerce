import { apiGet, apiSend } from '@/lib/api-client';

import type { Cart, MergeLineInput, MergeResponse } from './types';

const CART = '/api/v1/cart';

export function fetchCart(signal?: AbortSignal): Promise<Cart> {
  return apiGet<Cart>(CART, { signal });
}

/** Adds to the line. The body carries the variant and the quantity only: the server prices the cart. */
export function addCartItem(variantId: string, quantity: number): Promise<Cart> {
  return apiSend<Cart>('POST', `${CART}/items`, { body: { variantId, quantity } });
}

export function setCartItem(variantId: string, quantity: number): Promise<Cart> {
  return apiSend<Cart>('PUT', `${CART}/items/${encodeURIComponent(variantId)}`, { body: { quantity } });
}

/** 204: there is no cart in the reply, so the caller refetches. */
export function removeCartItem(variantId: string): Promise<unknown> {
  return apiSend<unknown>('DELETE', `${CART}/items/${encodeURIComponent(variantId)}`);
}

/** The same key must be sent again when a merge is retried (arch 8.1). */
export function mergeCart(lines: MergeLineInput[], idempotencyKey: string): Promise<MergeResponse> {
  return apiSend<MergeResponse>('POST', `${CART}/merge`, {
    body: { lines },
    headers: { 'Idempotency-Key': idempotencyKey },
  });
}
