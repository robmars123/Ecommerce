// Wire types for the admin order and returns API (arch 9, Slice 6 D14). Money is a string formatted by the server; the UI never adds it up.
import type { Money } from '@/features/catalog/types';
import type { OrderDetail, OrderStatus, RefundStatus } from '@/features/orders/types';

export type AdminRefund = { refundId: string; amount: Money; status: RefundStatus; createdAt: string };

export type ReturnStatus = 'Requested' | 'Approved' | 'Declined' | 'Refunded';

export type ReturnRequestRow = {
  id: string;
  orderId: string;
  orderNumber: string;
  status: ReturnStatus;
  reason: string;
  adminNote: string | null;
  items: { variantId: string; name: string; quantity: number }[];
  createdAt: string;
  decidedAt: string | null;
};

export type AdminOrderDetail = {
  order: OrderDetail;
  userId: string;
  refunds: AdminRefund[];
  refundedTotal: Money;
  pendingTotal: Money;
  remaining: Money;
  returnRequests: ReturnRequestRow[];
};

export type ReturnListPage = { items: ReturnRequestRow[]; page: number; pageSize: number; totalCount: number; totalPages: number };

export type RefundBody = { amount: Money; reason: string };

/** The 202 answer: always Pending, with the totals the server computed. */
export type RefundAccepted = { refundId: string; amount: Money; status: 'Pending'; refundedTotal: Money; remaining: Money };

// Slice 5 gap-fill: the admin order list and the fulfillment actions (arch 13.2 "Admin order list and actions").
export type AdminOrderListItem = {
  id: string;
  orderNumber: string;
  customerEmail: string | null;
  createdAt: string;
  status: OrderStatus;
  grandTotal: Money;
};

export type AdminOrderListPage = { items: AdminOrderListItem[]; page: number; pageSize: number; totalCount: number; totalPages: number };

export type AdminOrderFilters = { status: OrderStatus | 'all'; from: string; to: string; customer: string; orderNumber: string };

export type AdminOrderAction =
  | { kind: 'process' }
  | { kind: 'ship'; carrierCode: string; trackingNumber: string }
  | { kind: 'deliver' }
  | { kind: 'override'; toStatus: 'Processing' | 'Delivered'; reason: string };
