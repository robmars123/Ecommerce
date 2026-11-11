// Wire types for the admin customers API (arch 9, app-context 12.1). Money is a string formatted by the server; the UI never adds it up.
import type { ReturnStatus } from '@/features/admin-orders/types';
import type { Money } from '@/features/catalog/types';
import type { OrderStatus, RefundStatus } from '@/features/orders/types';

export type AdminCustomerListItem = { id: string; email: string; displayName: string; createdAt: string };

export type AdminCustomerListPage = { items: AdminCustomerListItem[]; page: number; pageSize: number; totalCount: number; totalPages: number };

export type AdminCustomerOrder = { id: string; orderNumber: string; createdAt: string; status: OrderStatus; grandTotal: Money };

export type AdminCustomerReturn = {
  id: string;
  orderId: string;
  orderNumber: string;
  status: ReturnStatus;
  reason: string;
  createdAt: string;
  decidedAt: string | null;
};

export type AdminCustomerRefund = {
  id: string;
  orderId: string;
  orderNumber: string;
  amount: Money;
  status: RefundStatus;
  automatic: boolean;
  createdAt: string;
};

/** totalSpent is null when the server withholds it (Support). The screen shows it only when present. */
export type AdminCustomerDetail = {
  id: string;
  email: string;
  displayName: string;
  createdAt: string;
  orderCount: number;
  totalSpent: Money | null;
  recentOrders: AdminCustomerOrder[];
  returnCount: number;
  recentReturns: AdminCustomerReturn[];
  refundCount: number;
  recentRefunds: AdminCustomerRefund[];
};
