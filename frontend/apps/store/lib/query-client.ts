import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query';

import { ApiError } from './api-client';
import { logClientError } from './logger';

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

/** Leading string segments of a key, at most two: ['products','detail',slug] -> 'products.detail'. Never params. */
function sourceFor(key: readonly unknown[] | undefined, fallback: string): string {
  const names: string[] = [];
  for (const part of key ?? []) {
    if (typeof part !== 'string' || names.length === 2) {
      break;
    }
    names.push(part);
  }
  return names.length > 0 ? names.join('.') : fallback;
}

export function createQueryClient(): QueryClient {
  return new QueryClient({
    // onError runs once per final failure, after retries.
    queryCache: new QueryCache({ onError: (error, query) => logClientError(error, sourceFor(query.queryKey, 'query')) }),
    mutationCache: new MutationCache({
      onError: (error, _variables, _context, mutation) => logClientError(error, sourceFor(mutation.options.mutationKey, 'mutation')),
    }),
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        refetchOnWindowFocus: false,
        retry: shouldRetry,
      },
    },
  });
}
