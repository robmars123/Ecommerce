import type { Money } from '@/features/catalog/types';

/**
 * Formats a server-calculated money value for display. Formatting only: the client never does price math.
 * A non-numeric amount falls back to "<amount> <currency>" rather than showing a wrong number.
 */
export function formatMoney(money: Money, locale = 'en-US'): string {
  const value = Number(money.amount);
  if (money.amount.trim() === '' || !Number.isFinite(value)) {
    return `${money.amount} ${money.currency}`;
  }
  try {
    return new Intl.NumberFormat(locale, { style: 'currency', currency: money.currency }).format(value);
  } catch {
    return `${money.amount} ${money.currency}`;
  }
}
