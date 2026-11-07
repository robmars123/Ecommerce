import { ApiError, apiGet, apiSend, type ApiErrorKind } from './api-client';
import { queryKeys } from './query-keys';
import { createQueryClient, shouldRetry } from './query-client';
import { problemResponse } from '@/tests/fetch-mock';

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

describe('createQueryClient error logging', () => {
  let errorSpy: jest.SpyInstance;

  beforeEach(() => {
    process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
    errorSpy = jest.spyOn(console, 'error').mockImplementation(() => undefined);
  });

  afterEach(() => {
    jest.restoreAllMocks();
    delete process.env.EXPO_PUBLIC_API_BASE_URL;
  });

  it('logs a failed products query once after retries, by static source, without the search term', async () => {
    const fetchSpy = jest
      .spyOn(globalThis, 'fetch')
      .mockImplementation(async () => problemResponse(500, { correlationId: 'corr-9' }));
    const client = createQueryClient();
    client.setDefaultOptions({ queries: { ...client.getDefaultOptions().queries, retryDelay: 0 } });

    await client
      .fetchQuery({
        queryKey: queryKeys.products.list({ search: 'secret term', page: 1, pageSize: 8 }),
        queryFn: () => apiGet('/api/v1/products', { params: { search: 'secret term' } }),
      })
      .catch(() => undefined);

    expect(fetchSpy).toHaveBeenCalledTimes(3);
    expect(errorSpy).toHaveBeenCalledTimes(1);
    expect(errorSpy).toHaveBeenCalledWith('[client-error]', {
      source: 'products.list',
      kind: 'server',
      status: 500,
      correlationId: 'corr-9',
    });
    expect(JSON.stringify(errorSpy.mock.calls[0])).not.toContain('secret');
  });

  it('logs a failed mutation once', async () => {
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => problemResponse(500, { correlationId: 'corr-8' }));
    const client = createQueryClient();

    await client
      .getMutationCache()
      .build(client, { mutationFn: () => apiSend('POST', '/api/v1/things', { body: { a: 1 } }) })
      .execute(undefined)
      .catch(() => undefined);

    expect(errorSpy).toHaveBeenCalledTimes(1);
    expect(errorSpy.mock.calls[0][1]).toMatchObject({ source: 'mutation', kind: 'server', correlationId: 'corr-8' });
  });
});
