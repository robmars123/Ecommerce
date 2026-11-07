import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useRef, useSyncExternalStore } from 'react';

import { useSession } from '@/features/auth/hooks/useSession';
import { ApiError } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';

import { mergeCart } from '../api';
import { useGuestCartStore } from '../guest-cart-store';
import { useMergeResultStore } from '../merge-result-store';

function useHydrated(): boolean {
  return useSyncExternalStore(
    (listener) => useGuestCartStore.persist.onFinishHydration(listener),
    () => useGuestCartStore.persist.hasHydrated(),
    () => true,
  );
}

/**
 * Merges the device cart into the account once the user is signed in (arch 8.1, F9). One request at a time. The
 * device cart is cleared only after a 200. A failure keeps the lines and the key, so a retry reuses the same key; a 409
 * drops the key so the retry gets a new one. The outcome goes to the merge result store.
 */
export function useGuestCartMerge(): void {
  const session = useSession();
  const hydrated = useHydrated();
  const queryClient = useQueryClient();
  const hasLines = useGuestCartStore((s) => s.lines.length > 0);
  const inFlight = useRef(false);
  const failedRef = useRef(false);

  const run = useCallback(async () => {
    const store = useGuestCartStore.getState();
    if (inFlight.current || store.lines.length === 0) {
      return;
    }
    inFlight.current = true;
    failedRef.current = false;
    useMergeResultStore.getState().clearFailure();
    const lines = store.lines;
    const key = store.ensureMergeKey();
    try {
      const response = await mergeCart(lines.map((l) => ({ variantId: l.variantId, quantity: l.quantity })), key);
      queryClient.setQueryData(queryKeys.cart, response.cart);
      const names = new Map(lines.map((l) => [l.variantId, `${l.snapshot.productName} - ${l.snapshot.variantName}`]));
      useMergeResultStore.getState().publish(response.results.map((r) => ({ ...r, name: names.get(r.variantId) ?? 'Item' })));
      useGuestCartStore.getState().clearAfterMerge();
    } catch (error) {
      if (error instanceof ApiError && error.kind === 'conflict') {
        useGuestCartStore.setState({ mergeKey: null });
      }
      failedRef.current = true;
      useMergeResultStore.getState().fail(() => void run());
    } finally {
      inFlight.current = false;
    }
  }, [queryClient]);

  useEffect(() => {
    if (session.status === 'signed-in' && hydrated && hasLines && !failedRef.current) {
      void run();
    }
  }, [session.status, hydrated, hasLines, run]);
}
