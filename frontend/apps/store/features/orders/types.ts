// Wire types for the orders API (arch 9, Orders contract). Money amounts are strings formatted by the server.

import type { CartImage } from '@/features/cart/types';
import type { Money } from '@/features/catalog/types';

export type OrderStatus = 'PendingPayment' | 'Paid' | 'Processing' | 'Shipped' | 'Delivered' | 'Cancelled' | 'Refunded' | 'Returned';

/** The latest payment attempt (arch 9 orders contract). Null before any attempt. */
export type PaymentStatus = 'Pending' | 'Succeeded' | 'Failed' | 'PartiallyRefunded' | 'Refunded';

export type OrderSummary = {
  id: string;
  orderNumber: string;
  createdAt: string;
  status: OrderStatus;
  grandTotal: Money;
  trackingUrl: string | null;
};

export type OrderListPage = {
  items: OrderSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type OrderItem = {
  variantId: string;
  sku: string;
  name: string;
  unitPrice: Money;
  quantity: number;
  lineTotal: Money;
};

/** An order line on the detail page: the snapshot plus today's Catalog thumbnail and colour (display only, null when unknown). */
export type OrderDetailItem = OrderItem & { image: CartImage | null; colour: string | null };

export type ShippingAddress = {
  fullName: string;
  line1: string;
  line2: string | null;
  city: string;
  region: string | null;
  postalCode: string;
  country: string;
};

export type TimelineEntry = {
  fromStatus: OrderStatus | null;
  toStatus: OrderStatus;
  at: string;
  reason: string | null;
};

export type Shipment = {
  carrierName: string;
  trackingNumber: string;
  trackingUrl: string | null;
  shippedAt: string;
  deliveredAt: string | null;
};

/** The money back to the customer (arch 8.5). Pending until the payment provider confirms it. */
export type RefundStatus = 'Pending' | 'Succeeded' | 'Failed';

export type Refund = { amount: Money; status: RefundStatus; createdAt: string };

export type ReturnItemBody = { variantId: string; quantity: number };

export type ReturnRequestBody = { items: ReturnItemBody[]; reason: string };

export type OrderAlert = { title: string; message: string };

export type StatusNote = { pillNote: string; alert: OrderAlert | null };

export type PaymentMethod = 'card';

/** The payment card: amounts are computed by the server; Captured is webhook-confirmed money only. */
export type OrderPayment = { method: PaymentMethod; amountDue: Money; captured: Money };

export type OrderDetail = {
  id: string;
  orderNumber: string;
  createdAt: string;
  status: OrderStatus;
  items: OrderDetailItem[];
  subtotal: Money;
  discountTotal: Money;
  shippingTotal: Money;
  taxTotal: Money;
  grandTotal: Money;
  shippingMethod: { code: string; name: string };
  shippingAddress: ShippingAddress;
  timeline: TimelineEntry[];
  shipments: Shipment[];
  paymentStatus: PaymentStatus | null;
  refunds: Refund[];
  statusNote: StatusNote | null;
  payment: OrderPayment | null;
};
