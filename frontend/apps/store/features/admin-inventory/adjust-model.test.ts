import { ApiError } from '@/lib/api-client';
import { makeInventoryItem, makeInventoryPage } from '@/tests/fixtures';

import { buildAdjustRequest, conflictMessage, patchInventoryPage, validateAdjust } from './adjust-model';

describe('validateAdjust', () => {
  it.each(['', '0', '-1', '1.5', '1e3', '1000001', ' 5'])('rejects quantity %j', (quantity) => {
    expect(validateAdjust(quantity, 'ok').quantity).toBeDefined();
  });

  it.each(['1', '1000000'])('accepts quantity %j', (quantity) => {
    expect(validateAdjust(quantity, 'ok')).toEqual({});
  });

  it('rejects an empty, blank or 501-char reason and accepts 500', () => {
    expect(validateAdjust('1', '').reason).toBeDefined();
    expect(validateAdjust('1', '   ').reason).toBeDefined();
    expect(validateAdjust('1', 'a'.repeat(501)).reason).toBeDefined();
    expect(validateAdjust('1', 'a'.repeat(500))).toEqual({});
  });
});

describe('buildAdjustRequest', () => {
  const item = makeInventoryItem({ variantId: 'v-9', onHand: 12 });

  it('adds with a positive delta and removes with a negative one', () => {
    expect(buildAdjustRequest(item, '5', 'x', 'add').delta).toBe(5);
    expect(buildAdjustRequest(item, '5', 'x', 'remove').delta).toBe(-5);
  });

  it('sends the displayed on-hand as expectedOnHand and trims the reason', () => {
    expect(buildAdjustRequest(item, '5', '  Restock  ', 'add')).toEqual({
      variantId: 'v-9',
      delta: 5,
      expectedOnHand: 12,
      reason: 'Restock',
    });
  });
});

describe('patchInventoryPage', () => {
  const a = makeInventoryItem({ variantId: 'a', onHand: 1 });
  const b = makeInventoryItem({ variantId: 'b', onHand: 2 });
  const page = makeInventoryPage([a, b], { page: 3, totalCount: 42, totalPages: 5 });

  it('replaces only the matching item and keeps the paging fields', () => {
    const updated = { ...b, onHand: 9, available: 9 };
    const next = patchInventoryPage(page, updated);

    expect(next).toEqual({ ...page, items: [a, updated] });
    expect(next!.items[0]).toBe(a);
  });

  it('returns the same reference when no item matches, and undefined stays undefined', () => {
    expect(patchInventoryPage(page, makeInventoryItem({ variantId: 'zzz' }))).toBe(page);
    expect(patchInventoryPage(undefined, a)).toBeUndefined();
  });
});

describe('conflictMessage', () => {
  const conflict = (details: { onHand?: number; available?: number }) => new ApiError('conflict', 'Conflict', { status: 409, ...details });

  it('is stale when the server on-hand differs from the one sent, and names both numbers', () => {
    const result = conflictMessage(conflict({ onHand: 7, available: 7 }), 5);

    expect(result.stale).toBe(true);
    expect(result.message).toContain('7');
    expect(result.message).toContain('5');
  });

  it('is insufficient when on-hand equals the one sent, and names available', () => {
    const result = conflictMessage(conflict({ onHand: 5, available: 2 }), 5);

    expect(result.stale).toBe(false);
    expect(result.message).toContain('2 available');
  });
});
