import type { OrderStatus, PaymentStatus } from './types';

const LABELS: Record<OrderStatus, string> = {
  PendingPayment: 'Awaiting payment',
  Paid: 'Paid',
  Processing: 'Processing',
  Shipped: 'Shipped',
  Delivered: 'Delivered',
  Cancelled: 'Cancelled',
  Refunded: 'Refunded',
  Returned: 'Returned',
};

export function statusLabel(status: OrderStatus): string {
  return LABELS[status] ?? status;
}

const PAYMENT_LABELS: Record<PaymentStatus, string> = {
  Pending: 'Awaiting payment',
  Succeeded: 'Paid',
  Failed: 'Payment failed',
  PartiallyRefunded: 'Partly refunded',
  Refunded: 'Refunded',
};

export function paymentStatusLabel(status: PaymentStatus): string {
  return PAYMENT_LABELS[status] ?? status;
}

/** A fixed UTC date so the same order shows the same day on every device. */
export function formatOrderDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', { timeZone: 'UTC', year: 'numeric', month: 'short', day: 'numeric' });
}
