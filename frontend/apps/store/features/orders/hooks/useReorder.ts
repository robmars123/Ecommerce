import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useRef, useState } from 'react';

import { mergeCart } from '@/features/cart/api';
import type { MergeLineInput, MergeLineResult } from '@/features/cart/types';
import { ApiError, userMessage } from '@/lib/api-client';
import { newIdempotencyKey } from '@/lib/idempotency-key';
import { queryKeys } from '@/lib/query-keys';

import type { OrderItem } from '../types';

export type ReorderLine = MergeLineResult & { name: string };
export type ReorderState =
  { status: 'idle' } | { status: 'pending' } | { status: 'done'; lines: ReorderLine[] } | { status: 'failed'; message: string };

/** One line per variant (quantities summed, first-occurrence order). Only variantId and quantity: the server prices every line. */
export function reorderLines(items: OrderItem[]): MergeLineInput[] {
  const lines: MergeLineInput[] = [];
  for (const item of items) {
    const existing = lines.find((l) => l.variantId === item.variantId);
    if (existing) {
      existing.quantity += item.quantity;
    } else {
      lines.push({ variantId: item.variantId, quantity: item.quantity });
    }
  }
  return lines;
}

/**
 * Puts an order's items back in the cart through the cart merge (arch 8.1). One key per press, kept across failed attempts so a
 * retry replays instead of doubling; a 409 drops it. A second press while a request is in flight, or after success, does nothing.
 */
export function useReorder(items: OrderItem[]): { state: ReorderState; reorder: () => void } {
  const queryClient = useQueryClient();
  const [state, setState] = useState<ReorderState>({ status: 'idle' });
  const inFlight = useRef(false);
  const done = useRef(false);
  const keyRef = useRef<string | null>(null);

  const reorder = useCallback(() => {
    if (inFlight.current || done.current) {
      return;
    }
    inFlight.current = true;
    keyRef.current ??= newIdempotencyKey();
    setState({ status: 'pending' });
    void (async () => {
      try {
        const response = await mergeCart(reorderLines(items), keyRef.current as string);
        queryClient.setQueryData(queryKeys.cart, response.cart);
        const names = new Map(items.map((i) => [i.variantId, i.name]));
        done.current = true;
        setState({ status: 'done', lines: response.results.map((r) => ({ ...r, name: names.get(r.variantId) ?? 'Item' })) });
      } catch (error) {
        if (error instanceof ApiError && error.kind === 'conflict') {
          keyRef.current = null;
        }
        setState({ status: 'failed', message: userMessage(error) });
      } finally {
        inFlight.current = false;
      }
    })();
  }, [items, queryClient]);

  return { state, reorder };
}
