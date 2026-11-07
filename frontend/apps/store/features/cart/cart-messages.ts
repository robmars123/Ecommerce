import { ApiError, userMessage } from '@/lib/api-client';

import type { CartIssue, MergeLineResult } from './types';

export const ISSUE_TEXT: Record<CartIssue, string> = {
  PriceChanged: 'The price changed since you added this.',
  ExceedsStock: 'There is less in stock than you have in your cart. Lower the quantity.',
  Unavailable: 'This item is no longer available. Remove it to continue.',
};

const REASON_TEXT: Record<NonNullable<MergeLineResult['reason']>, string> = {
  Unavailable: 'no longer available',
  OutOfStock: 'not enough in stock',
  LimitReached: 'limit per item reached',
};

export function mergeResultText(result: Pick<MergeLineResult, 'outcome' | 'quantity' | 'reason'>): string {
  const reason = result.reason ? REASON_TEXT[result.reason] : '';
  switch (result.outcome) {
    case 'Kept':
      return `${result.quantity} in your cart`;
    case 'Capped':
      return `reduced to ${result.quantity} (${reason})`;
    default:
      return `not added (${reason})`;
  }
}

/** A stock conflict names the quantity the server has; anything else uses the shared plain-language copy. */
export function cartErrorMessage(error: unknown): string {
  if (error instanceof ApiError && error.kind === 'conflict' && error.available !== undefined) {
    return error.available === 0 ? 'This item is out of stock.' : `Only ${error.available} available.`;
  }
  return userMessage(error);
}
