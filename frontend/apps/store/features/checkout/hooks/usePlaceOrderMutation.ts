import { useMutation } from '@tanstack/react-query';
import { useRef } from 'react';

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
  const mutation = useMutation<CheckoutResponse, Error, CheckoutBody>({
    mutationFn: (body) => placeOrder(body, key.current()),
    onSuccess: () => key.renew(),
    onError: (error) => {
      if (isDefinitiveFailure(error)) {
        key.renew();
      }
    },
  });

  function submit(body: CheckoutBody, onPlaced?: (response: CheckoutResponse) => void): void {
    if (inFlight.current) {
      return;
    }
    inFlight.current = true;
    mutation.mutate(body, {
      onSuccess: onPlaced,
      onSettled: () => {
        inFlight.current = false;
      },
    });
  }

  return { submit, isPending: mutation.isPending, error: mutation.error, reset: mutation.reset };
}
