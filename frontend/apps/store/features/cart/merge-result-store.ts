import { create } from 'zustand';

import type { MergeLineResult } from './types';

export type MergeNoticeLine = MergeLineResult & { name: string };

export type MergeResultState = {
  /** The last merge's per-line results (never persisted). Null when none is showing. */
  results: MergeNoticeLine[] | null;
  /** The merge could not be completed; the device cart is still on this device. */
  failed: boolean;
  retry: (() => void) | null;
  publish: (results: MergeNoticeLine[]) => void;
  fail: (retry: () => void) => void;
  clearFailure: () => void;
  dismiss: () => void;
};

export const useMergeResultStore = create<MergeResultState>()((set) => ({
  results: null,
  failed: false,
  retry: null,
  publish: (results) => set({ results, failed: false, retry: null }),
  fail: (retry) => set({ failed: true, retry }),
  clearFailure: () => set({ failed: false, retry: null }),
  dismiss: () => set({ results: null, failed: false, retry: null }),
}));
