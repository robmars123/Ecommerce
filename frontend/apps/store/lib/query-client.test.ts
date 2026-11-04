import { ApiError, type ApiErrorKind } from './api-client';
import { createQueryClient, shouldRetry } from './query-client';

describe('shouldRetry', () => {
  it.each<ApiErrorKind>(['not-found', 'validation', 'client', 'config'])('never retries %s', (kind) => {
    expect(shouldRetry(0, new ApiError(kind, ''))).toBe(false);
    expect(shouldRetry(1, new ApiError(kind, ''))).toBe(false);
  });

  it.each<ApiErrorKind>(['network', 'server', 'timeout'])('retries %s up to two times', (kind) => {
    expect(shouldRetry(0, new ApiError(kind, ''))).toBe(true);
    expect(shouldRetry(1, new ApiError(kind, ''))).toBe(true);
    expect(shouldRetry(2, new ApiError(kind, ''))).toBe(false);
  });

  it('does not retry an error that is not an ApiError', () => {
    expect(shouldRetry(0, new Error('bug'))).toBe(false);
  });
});

describe('createQueryClient', () => {
  it('uses a 30s stale time and does not refetch on window focus', () => {
    const defaults = createQueryClient().getDefaultOptions().queries;

    expect(defaults?.staleTime).toBe(30_000);
    expect(defaults?.refetchOnWindowFocus).toBe(false);
    expect(defaults?.retry).toBe(shouldRetry);
  });

  it('returns a new client each call so state is never shared between app instances or tests', () => {
    expect(createQueryClient()).not.toBe(createQueryClient());
  });
});
