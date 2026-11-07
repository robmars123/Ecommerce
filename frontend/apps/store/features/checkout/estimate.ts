import type { Money } from '@/features/catalog/types';

function toCents(amount: string): number | null {
  const match = /^(\d+)(?:\.(\d{1,2}))?$/.exec(amount.trim());
  return match ? Number(match[1]) * 100 + Number((match[2] ?? '').padEnd(2, '0')) : null;
}

/**
 * The estimate shown before checkout: cart subtotal plus the chosen method price, in whole cents. Display only. Tax is not
 * included and the screen says so; the server total from the checkout reply replaces it (F6, F10). Null when the two
 * amounts cannot be added exactly (other currencies or an unreadable amount): the screen then shows no estimate.
 */
export function estimateTotal(subtotal: Money, shipping: Money): Money | null {
  const a = toCents(subtotal.amount);
  const b = toCents(shipping.amount);
  if (a === null || b === null || subtotal.currency !== shipping.currency) {
    return null;
  }
  const cents = a + b;
  return { amount: `${Math.floor(cents / 100)}.${String(cents % 100).padStart(2, '0')}`, currency: subtotal.currency };
}
