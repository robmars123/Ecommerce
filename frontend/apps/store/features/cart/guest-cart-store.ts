import AsyncStorage from '@react-native-async-storage/async-storage';
import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';

import { newIdempotencyKey } from '@/lib/idempotency-key';

import type { GuestLine } from './types';

export const MAX_GUEST_LINE_QUANTITY = 99;
export const MAX_GUEST_LINES = 100;

export type GuestCartState = {
  lines: GuestLine[];
  /** The Idempotency-Key of the merge in progress. Any line edit drops it: a key reused with a changed body would be a 409. */
  mergeKey: string | null;
  add: (line: Omit<GuestLine, 'quantity'>, quantity: number) => void;
  setQuantity: (variantId: string, quantity: number) => void;
  remove: (variantId: string) => void;
  /** Only after the server answered 200 to the merge. */
  clearAfterMerge: () => void;
  ensureMergeKey: () => string;
};

function clamp(quantity: number): number {
  return Math.min(MAX_GUEST_LINE_QUANTITY, Math.max(1, Math.floor(quantity)));
}

/** A factory so tests can start a fresh store (a restart) over the same mocked storage. */
export function createGuestCartStore() {
  return create<GuestCartState>()(
    persist(
      (set, get) => ({
        lines: [],
        mergeKey: null,
        add: (line, quantity) =>
          set((state) => {
            const existing = state.lines.find((l) => l.variantId === line.variantId);
            if (existing) {
              return {
                mergeKey: null,
                lines: state.lines.map((l) =>
                  l.variantId === line.variantId ? { ...l, snapshot: line.snapshot, quantity: clamp(l.quantity + quantity) } : l,
                ),
              };
            }
            if (state.lines.length >= MAX_GUEST_LINES) {
              return state;
            }
            return { mergeKey: null, lines: [...state.lines, { ...line, quantity: clamp(quantity) }] };
          }),
        setQuantity: (variantId, quantity) =>
          set((state) => ({
            mergeKey: null,
            lines:
              quantity <= 0
                ? state.lines.filter((l) => l.variantId !== variantId)
                : state.lines.map((l) => (l.variantId === variantId ? { ...l, quantity: clamp(quantity) } : l)),
          })),
        remove: (variantId) =>
          set((state) => ({ mergeKey: null, lines: state.lines.filter((l) => l.variantId !== variantId) })),
        clearAfterMerge: () => set({ lines: [], mergeKey: null }),
        ensureMergeKey: () => {
          const existing = get().mergeKey;
          if (existing) {
            return existing;
          }
          const key = newIdempotencyKey();
          set({ mergeKey: key });
          return key;
        },
      }),
      {
        name: 'guest-cart',
        version: 1,
        storage: createJSONStorage(() => AsyncStorage),
        partialize: (state) => ({ lines: state.lines, mergeKey: state.mergeKey }),
      },
    ),
  );
}

/** The app's one device cart. */
export const useGuestCartStore = createGuestCartStore();
