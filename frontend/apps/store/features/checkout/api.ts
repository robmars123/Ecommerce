import { apiGet, apiSend } from '@/lib/api-client';

import type { CheckoutBody, CheckoutResponse, RetryPaymentResponse, ShippingMethodsResponse } from './types';

export function fetchShippingMethods(signal?: AbortSignal): Promise<ShippingMethodsResponse> {
  return apiGet<ShippingMethodsResponse>('/api/v1/shipping-methods', { signal });
}

/** The same key must be sent again when this exact attempt is retried after a network failure (arch 8.2). */
export function placeOrder(body: CheckoutBody, idempotencyKey: string): Promise<CheckoutResponse> {
  return apiSend<CheckoutResponse>('POST', '/api/v1/checkout', { body, headers: { 'Idempotency-Key': idempotencyKey } });
}

export function retryPayment(orderId: string, idempotencyKey: string): Promise<RetryPaymentResponse> {
  return apiSend<RetryPaymentResponse>('POST', `/api/v1/orders/${encodeURIComponent(orderId)}/retry-payment`, {
    headers: { 'Idempotency-Key': idempotencyKey },
  });
}
