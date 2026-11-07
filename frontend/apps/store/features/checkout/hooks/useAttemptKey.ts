import { useMemo, useRef } from 'react';

import { ApiError } from '@/lib/api-client';
import { newIdempotencyKey } from '@/lib/idempotency-key';

/**
 * One idempotency key per logical attempt (arch 8.2, F4). The key is made on first use and then kept, so a double
 * submit or a retry after a network failure sends the same key and the server returns the same result. `renew()`
 * is called after a definitive outcome: the next submit is a new attempt with a new key.
 */
export function useAttemptKey() {
  const ref = useRef<string | null>(null);
  return useMemo(
    () => ({
      current(): string {
        ref.current ??= newIdempotencyKey();
        return ref.current;
      },
      renew(): void {
        ref.current = null;
      },
    }),
    [],
  );
}

/**
 * The server answered and the answer will not change by repeating the request (a 4xx). A network failure, a timeout, a
 * 5xx (including 503 payment-provider-unavailable) or an expired session is not definitive: repeat with the same key.
 */
export function isDefinitiveFailure(error: unknown): boolean {
  return error instanceof ApiError && ['validation', 'conflict', 'forbidden', 'not-found', 'client'].includes(error.kind);
}
