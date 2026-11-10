import type { ApiError } from '@/lib/api-client';

import type { AdjustStockRequest, AdminInventoryItem, AdminInventoryPage } from './types';

const MAX_QUANTITY = 1_000_000;
const MAX_REASON = 500;

/** Mirrors the server's 400 rules only. "Would go negative" is left to the server, which also audits that attempt. */
export function validateAdjust(quantity: string, reason: string): { quantity?: string; reason?: string } {
  const errors: { quantity?: string; reason?: string } = {};
  const amount = /^\d+$/.test(quantity) ? Number(quantity) : 0;
  if (amount < 1 || amount > MAX_QUANTITY) {
    errors.quantity = 'Enter a whole number from 1 to 1,000,000.';
  }
  const trimmed = reason.trim();
  if (trimmed.length < 1 || trimmed.length > MAX_REASON) {
    errors.reason = 'Enter a reason of 1 to 500 characters.';
  }
  return errors;
}

/** expectedOnHand is the on-hand number the admin sees on the row, so a concurrent change fails as stale. */
export function buildAdjustRequest(
  item: AdminInventoryItem,
  quantity: string,
  reason: string,
  direction: 'add' | 'remove',
): AdjustStockRequest {
  const amount = Number(quantity);
  return {
    variantId: item.variantId,
    delta: direction === 'add' ? amount : -amount,
    expectedOnHand: item.onHand,
    reason: reason.trim(),
  };
}

/** Replaces the matching item with the server's response; returns the same reference when nothing matches. */
export function patchInventoryPage(page: AdminInventoryPage | undefined, item: AdminInventoryItem): AdminInventoryPage | undefined {
  if (!page || !page.items.some((i) => i.variantId === item.variantId)) {
    return page;
  }
  return { ...page, items: page.items.map((i) => (i.variantId === item.variantId ? item : i)) };
}

/** Stale: the server's on-hand is not the one we sent. Otherwise the removal would take available below zero. */
export function conflictMessage(error: ApiError, expectedOnHand: number): { message: string; stale: boolean } {
  if (error.onHand !== undefined && error.onHand !== expectedOnHand) {
    return {
      message: `On hand is now ${error.onHand}, not ${expectedOnHand}. Someone else changed it. Reload to see the current numbers.`,
      stale: true,
    };
  }
  return {
    message:
      error.available !== undefined
        ? `Only ${error.available} available; reserved stock cannot be removed.`
        : 'This change conflicts with the current stock.',
    stale: false,
  };
}
