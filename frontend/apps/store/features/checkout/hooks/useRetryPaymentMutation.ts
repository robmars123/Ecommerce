import { useMutation } from '@tanstack/react-query';
import { useRef } from 'react';

import { retryPayment } from '../api';
import type { RetryPaymentResponse } from '../types';
import { isDefinitiveFailure, useAttemptKey } from './useAttemptKey';

/** A new payment attempt on the same order. One key per retry; a network failure repeats with the same key (F4, F11). */
export function useRetryPaymentMutation(orderId: string) {
  const key = useAttemptKey();
  const inFlight = useRef(false);
  const mutation = useMutation<RetryPaymentResponse, Error, void>({
    mutationFn: () => retryPayment(orderId, key.current()),
    onSuccess: () => key.renew(),
    onError: (error) => {
      if (isDefinitiveFailure(error)) {
        key.renew();
      }
    },
  });

  function retry(onSecret: (clientSecret: string) => void): void {
    if (inFlight.current) {
      return;
    }
    inFlight.current = true;
    mutation.mutate(undefined, {
      onSuccess: (response) => onSecret(response.clientSecret),
      onSettled: () => {
        inFlight.current = false;
      },
    });
  }

  return { retry, isPending: mutation.isPending, error: mutation.error, reset: mutation.reset };
}
