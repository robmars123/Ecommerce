import { act, renderHook, waitFor } from '@testing-library/react-native';

import { ApiError } from '@/lib/api-client';
import { calledUrls, jsonResponse, mockFetchRoutes, problemResponse } from '@/tests/fetch-mock';
import { categories, makeDetail, makeItem, makePage } from '@/tests/fixtures';
import { createTestQueryClient, queryWrapper } from '@/tests/render-with-query';

import { useCategoriesQuery } from './useCategoriesQuery';
import { useDebouncedValue } from './useDebouncedValue';
import { useProductQuery } from './useProductQuery';
import { useProductsQuery } from './useProductsQuery';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  jest.useRealTimers();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

describe('useProductsQuery', () => {
  it('returns the page from the server and sends every filter as a query param', async () => {
    const spy = mockFetchRoutes({ '/api/v1/products': () => jsonResponse(200, makePage([makeItem()])) });
    const client = createTestQueryClient();

    const { result } = await renderHook(
      () => useProductsQuery({ search: 'shirt', category: 'tops', sort: 'price-asc', page: 2, pageSize: 20 }),
      { wrapper: queryWrapper(client) },
    );

    await waitFor(() => expect(result.current.data?.items[0].name).toBe('Blue Shirt'));
    expect(calledUrls(spy)).toEqual([
      'http://api.test.invalid:5080/api/v1/products?search=shirt&category=tops&sort=price-asc&page=2&pageSize=20',
    ]);
  });

  it('keeps showing the previous page while the next one loads', async () => {
    let release: () => void = () => undefined;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      if (String(input).includes('page=2')) {
        await gate;
        return jsonResponse(200, makePage([makeItem({ id: 'p-2', name: 'Second' })], { page: 2 }));
      }
      return jsonResponse(200, makePage([makeItem()]));
    });
    const client = createTestQueryClient();

    const { result, rerender } = await renderHook(
      ({ page }: { page: number }) => useProductsQuery({ page, pageSize: 20 }),
      { wrapper: queryWrapper(client), initialProps: { page: 1 } },
    );
    await waitFor(() => expect(result.current.data?.items[0].name).toBe('Blue Shirt'));

    await rerender({ page: 2 });
    expect(result.current.data?.items[0].name).toBe('Blue Shirt');
    expect(result.current.isPlaceholderData).toBe(true);

    await act(async () => release());
    await waitFor(() => expect(result.current.data?.items[0].name).toBe('Second'));
  });
});

describe('useProductQuery', () => {
  it('returns the product by slug', async () => {
    const spy = mockFetchRoutes({ '/api/v1/products/blue-shirt': () => jsonResponse(200, makeDetail()) });

    const { result } = await renderHook(() => useProductQuery('blue-shirt'), {
      wrapper: queryWrapper(createTestQueryClient()),
    });

    await waitFor(() => expect(result.current.data?.name).toBe('Blue Shirt'));
    expect(calledUrls(spy)).toEqual(['http://api.test.invalid:5080/api/v1/products/blue-shirt']);
  });

  it('surfaces a 404 as an ApiError without retrying, using the real retry policy', async () => {
    const spy = mockFetchRoutes({ '/api/v1/products/nope': () => problemResponse(404, { title: 'Product not found' }) });
    const client = createTestQueryClient({ useRealRetryPolicy: true });

    const { result } = await renderHook(() => useProductQuery('nope'), { wrapper: queryWrapper(client) });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBeInstanceOf(ApiError);
    expect((result.current.error as ApiError).kind).toBe('not-found');
    expect(spy).toHaveBeenCalledTimes(1);
  });
});

describe('useCategoriesQuery', () => {
  it('returns the categories', async () => {
    mockFetchRoutes({ '/api/v1/categories': () => jsonResponse(200, { items: categories }) });

    const { result } = await renderHook(() => useCategoriesQuery(), {
      wrapper: queryWrapper(createTestQueryClient()),
    });

    await waitFor(() => expect(result.current.data?.items).toHaveLength(2));
  });

  it('surfaces a server error as an ApiError', async () => {
    mockFetchRoutes({ '/api/v1/categories': () => problemResponse(500, { title: 'boom' }) });

    const { result } = await renderHook(() => useCategoriesQuery(), {
      wrapper: queryWrapper(createTestQueryClient()),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as ApiError).kind).toBe('server');
  });
});

describe('useDebouncedValue', () => {
  it('updates only after the delay has passed without a new value', async () => {
    jest.useFakeTimers();
    const { result, rerender } = await renderHook(({ value }: { value: string }) => useDebouncedValue(value, 300), {
      initialProps: { value: 'a' },
    });
    expect(result.current).toBe('a');

    await rerender({ value: 'ab' });
    await act(async () => {
      await jest.advanceTimersByTimeAsync(299);
    });
    expect(result.current).toBe('a');

    await rerender({ value: 'abc' });
    await act(async () => {
      await jest.advanceTimersByTimeAsync(299);
    });
    expect(result.current).toBe('a');

    await act(async () => {
      await jest.advanceTimersByTimeAsync(1);
    });
    expect(result.current).toBe('abc');
  });
});
