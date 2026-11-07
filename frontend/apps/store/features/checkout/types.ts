// Wire types for the checkout API (arch 9, Checkout contract). Money amounts are strings formatted by the server.
import type { Money } from '@/features/catalog/types';
import type { OrderItem, OrderStatus, ShippingAddress } from '@/features/orders/types';

export type ShippingMethod = { code: string; name: string; price: Money; estimatedDays: number };

export type ShippingMethodsResponse = { items: ShippingMethod[] };

/** What the customer types. The server snapshots it on the order. */
export type AddressInput = {
  fullName: string;
  line1: string;
  line2: string;
  city: string;
  region: string;
  postalCode: string;
  country: string;
};

/** The request body: an address and a method code only. No price, total, tax or status ever travels (rule 1). */
export type CheckoutBody = {
  shippingAddress: { fullName: string; line1: string; line2?: string; city: string; region?: string; postalCode: string; country: string };
  shippingMethodCode: string;
};

export type CheckoutOrder = {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  items: OrderItem[];
  subtotal: Money;
  discountTotal: Money;
  shippingTotal: Money;
  taxTotal: Money;
  grandTotal: Money;
  shippingMethod: { code: string; name: string };
  shippingAddress: ShippingAddress;
};

/** `clientSecret` is null unless the order is still PendingPayment (a replay of a paid order has nothing to pay). */
export type CheckoutResponse = { order: CheckoutOrder; clientSecret: string | null };

export type RetryPaymentResponse = { orderId: string; attempt: number; clientSecret: string };
