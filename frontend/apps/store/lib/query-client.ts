import { QueryClient } from '@tanstack/react-query';

import { ApiError } from './api-client';

const MAX_RETRIES = 2;

/**
 * Retry transient failures only. A 404, 400, other 4xx or a missing configuration will not fix itself,
 * so retrying them only delays the error state.
 */
export function shouldRetry(failureCount: number, error: unknown): boolean {
  if (!(error instanceof ApiError)) {
    return false;
  }
  const transient = error.kind === 'network' || error.kind === 'server' || error.kind === 'timeout';
  return transient && failureCount < MAX_RETRIES;
}

export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        refetchOnWindowFocus: false,
        retry: shouldRetry,
      },
    },
  });
}
