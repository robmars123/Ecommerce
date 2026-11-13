import type { OrderStatus, PaymentStatus, RefundStatus } from './types';

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

export type StatusTone = 'success' | 'warning' | 'danger' | 'neutral';

const TONES: Record<OrderStatus, StatusTone> = {
  PendingPayment: 'warning',
  Paid: 'neutral',
  Processing: 'neutral',
  Shipped: 'neutral',
  Delivered: 'success',
  Cancelled: 'danger',
  Refunded: 'neutral',
  Returned: 'neutral',
};

export function statusTone(status: OrderStatus): StatusTone {
  return TONES[status] ?? 'neutral';
}

export function paymentTone(status: PaymentStatus): StatusTone {
  return status === 'Pending' ? 'warning' : status === 'Succeeded' ? 'success' : status === 'Failed' ? 'danger' : 'neutral';
}

export function paymentMethodLabel(method: string): string {
  return method === 'card' ? 'Card' : method;
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

const REFUND_LABELS: Record<RefundStatus, string> = {
  Pending: 'Being processed',
  Succeeded: 'Refunded',
  Failed: 'Could not be refunded',
};

export function refundStatusLabel(status: RefundStatus): string {
  return REFUND_LABELS[status] ?? status;
}

const CANCELLED_TOTALS_NOTE =
  'No payment is kept for this order. It was cancelled before payment was confirmed, and any payment that went through late is refunded automatically.';

/** Note under the Total; only Cancelled has one (13.2 "Order totals note"). */
export function totalsNote(status: OrderStatus): string | null {
  return status === 'Cancelled' ? CANCELLED_TOTALS_NOTE : null;
}
