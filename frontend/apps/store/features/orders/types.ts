// Wire types for the orders API (arch 9, Orders contract). Money amounts are strings formatted by the server.
// refunds (Slice 6) are not in the response yet and are deliberately absent here.
import type { Money } from '@/features/catalog/types';

export type OrderStatus =
  | 'PendingPayment'
  | 'Paid'
  | 'Processing'
  | 'Shipped'
  | 'Delivered'
  | 'Cancelled'
  | 'Refunded'
  | 'Returned';

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

export type OrderDetail = {
  id: string;
  orderNumber: string;
  createdAt: string;
  status: OrderStatus;
  items: OrderItem[];
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
};
