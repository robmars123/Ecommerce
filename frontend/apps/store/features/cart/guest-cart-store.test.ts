import AsyncStorage from '@react-native-async-storage/async-storage';

import { createGuestCartStore } from './guest-cart-store';
import type { GuestLine } from './types';

type NewLine = Omit<GuestLine, 'quantity'>;

function newLine(variantId: string): NewLine {
  return {
    variantId,
    productSlug: `slug-${variantId}`,
    snapshot: { productName: 'Blue Shirt', variantName: 'Medium', price: { amount: '24.50', currency: 'USD' }, image: null },
  };
}

// F9: the guest device cart. AsyncStorage is mocked globally (tests/jest-setup.js).
describe('guest cart store', () => {
  beforeEach(async () => {
    await AsyncStorage.clear();
  });

  it('keeps its lines across an app restart (a new store rehydrates from storage)', async () => {
    const first = createGuestCartStore();
    first.getState().add(newLine('v-1'), 2);
    first.getState().add(newLine('v-2'), 1);
    await new Promise((resolve) => setTimeout(resolve, 0));

    const restarted = createGuestCartStore();
    await restarted.persist.rehydrate();

    expect(restarted.getState().lines.map((l) => [l.variantId, l.quantity])).toEqual([
      ['v-1', 2],
      ['v-2', 1],
    ]);
  });

  it('add sums into an existing line and creates a missing one', () => {
    const store = createGuestCartStore();

    store.getState().add(newLine('v-1'), 2);
    store.getState().add(newLine('v-1'), 3);
    store.getState().add(newLine('v-2'), 1);

    expect(store.getState().lines.map((l) => [l.variantId, l.quantity])).toEqual([
      ['v-1', 5],
      ['v-2', 1],
    ]);
  });

  it('setQuantity sets the line, and 0 or remove deletes it', () => {
    const store = createGuestCartStore();
    store.getState().add(newLine('v-1'), 2);
    store.getState().add(newLine('v-2'), 2);
    store.getState().add(newLine('v-3'), 2);

    store.getState().setQuantity('v-1', 7);
    store.getState().setQuantity('v-2', 0);
    store.getState().remove('v-3');

    expect(store.getState().lines.map((l) => [l.variantId, l.quantity])).toEqual([['v-1', 7]]);
  });

  it('caps a line at 99 and the cart at 100 lines', () => {
    const store = createGuestCartStore();
    store.getState().add(newLine('v-1'), 90);
    store.getState().add(newLine('v-1'), 90);
    for (let i = 0; i < 105; i += 1) store.getState().add(newLine(`x-${i}`), 1);

    expect(store.getState().lines[0].quantity).toBe(99);
    expect(store.getState().lines).toHaveLength(100);
  });

  it('every edit drops the merge key, so a changed body never reuses a key', () => {
    const store = createGuestCartStore();
    store.getState().add(newLine('v-1'), 1);
    const key = store.getState().ensureMergeKey();
    expect(store.getState().ensureMergeKey()).toBe(key);

    store.getState().add(newLine('v-1'), 1);
    expect(store.getState().mergeKey).toBeNull();

    store.getState().ensureMergeKey();
    store.getState().setQuantity('v-1', 5);
    expect(store.getState().mergeKey).toBeNull();

    store.getState().ensureMergeKey();
    store.getState().remove('v-1');
    expect(store.getState().mergeKey).toBeNull();
    expect(store.getState().ensureMergeKey()).not.toBe(key);
  });

  it('clearAfterMerge empties the lines and the key', () => {
    const store = createGuestCartStore();
    store.getState().add(newLine('v-1'), 1);
    store.getState().ensureMergeKey();

    store.getState().clearAfterMerge();

    expect(store.getState().lines).toEqual([]);
    expect(store.getState().mergeKey).toBeNull();
  });
});
