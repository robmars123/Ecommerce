import { useMutation } from '@tanstack/react-query';
import { useRef, useState } from 'react';

import { placeOrder } from '../api';
import type { CheckoutBody, CheckoutResponse } from '../types';
import { isDefinitiveFailure, useAttemptKey } from './useAttemptKey';

/**
 * Places the order. No optimistic order (F5): the screen waits for the server. A second `submit` while one is in flight
 * is ignored, so a double tap sends one request; a retry after a network failure reuses the key, a new attempt after a
 * definitive answer (such as a 409 the customer fixed) gets a new one (F4).
 */
export function usePlaceOrderMutation() {
  const key = useAttemptKey();
  const inFlight = useRef(false);
  // The body is frozen together with the key: a key means "this exact request", so after an unresolved answer
  // (network failure, 503) the next submit resends the first body and the screen locks its fields.
  const [locked, setLocked] = useState<CheckoutBody | null>(null);
  const lockedRef = useRef<CheckoutBody | null>(null);
  function unlock() {
    lockedRef.current = null;
    setLocked(null);
  }
  const mutation = useMutation<CheckoutResponse, Error, CheckoutBody>({
    mutationFn: (body) => placeOrder(body, key.current()),
    onSuccess: () => {
      key.renew();
      unlock();
    },
    onError: (error) => {
      if (isDefinitiveFailure(error)) {
        key.renew();
        unlock();
      }
    },
  });

  function submit(body: CheckoutBody, onPlaced?: (response: CheckoutResponse) => void): void {
    if (inFlight.current) {
      return;
    }
    inFlight.current = true;
    const sent = lockedRef.current ?? body;
    lockedRef.current = sent;
    setLocked(sent);
    mutation.mutate(sent, {
      onSuccess: onPlaced,
      onSettled: () => {
        inFlight.current = false;
      },
    });
  }

  return { submit, locked, isPending: mutation.isPending, error: mutation.error, reset: mutation.reset };
}
