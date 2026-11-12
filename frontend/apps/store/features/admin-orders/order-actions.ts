import type { OrderStatus } from '@/features/orders/types';

import type { AdminOrderFilters } from './types';

export const MAX_TRACKING = 64;
export const MAX_REASON = 500;

export type OrderActionKind = 'process' | 'ship' | 'deliver' | 'override-processing' | 'override-delivered';

/** Which buttons to offer per server status (D9). Only a hint: the server decides every transition. */
export function actionsFor(status: OrderStatus): OrderActionKind[] {
  switch (status) {
    case 'Paid':
      return ['process', 'override-processing'];
    case 'Processing':
      return ['ship'];
    case 'Shipped':
      return ['deliver', 'override-delivered'];
    default:
      return [];
  }
}

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

function isRealDate(value: string): boolean {
  if (!DATE_PATTERN.test(value)) {
    return false;
  }
  const [year, month, day] = value.split('-').map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day;
}

export type FilterErrors = Partial<Record<'from' | 'to', string>>;

/** Empty dates are fine. Anything else must be a real yyyy-MM-dd day, and from must not follow to. */
export function validateFilters(filters: Pick<AdminOrderFilters, 'from' | 'to'>): FilterErrors {
  const from = filters.from.trim();
  const to = filters.to.trim();
  const errors: FilterErrors = {};
  if (from && !isRealDate(from)) {
    errors.from = 'Use a date like 2026-03-31.';
  }
  if (to && !isRealDate(to)) {
    errors.to = 'Use a date like 2026-03-31.';
  }
  if (!errors.from && !errors.to && from && to && from > to) {
    errors.from = 'From must be on or before To.';
  }
  return errors;
}

export function validateShip(carrierCode: string, trackingNumber: string): { carrier?: string; tracking?: string } {
  const tracking = trackingNumber.trim();
  return {
    ...(carrierCode.trim().length === 0 ? { carrier: 'Enter a carrier code.' } : {}),
    ...(tracking.length === 0 || tracking.length > MAX_TRACKING
      ? { tracking: `Enter a tracking number of 1 to ${MAX_TRACKING} characters.` }
      : {}),
  };
}

export function validateReason(reason: string): string | undefined {
  const length = reason.trim().length;
  return length === 0 || length > MAX_REASON ? `Enter a reason of 1 to ${MAX_REASON} characters.` : undefined;
}
