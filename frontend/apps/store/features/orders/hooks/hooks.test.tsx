import { renderHook, waitFor } from '@testing-library/react-native';

import { ApiError } from '@/lib/api-client';
import { calledUrls, jsonResponse, mockFetchRoutes, problemResponse } from '@/tests/fetch-mock';
import { makeOrderDetail, makeOrderPage, makeOrderSummary } from '@/tests/order-fixtures';
import { createTestQueryClient, queryWrapper } from '@/tests/render-with-query';

import { useOrderQuery } from './useOrderQuery';
import { useOrdersQuery } from './useOrdersQuery';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

describe('useOrdersQuery', () => {
  it('returns the page from the server for the requested page', async () => {
    const spy = mockFetchRoutes({ '/api/v1/orders': () => jsonResponse(200, makeOrderPage([makeOrderSummary()])) });

    const { result } = await renderHook(() => useOrdersQuery(2), { wrapper: queryWrapper(createTestQueryClient()) });

    await waitFor(() => expect(result.current.data?.items[0].orderNumber).toBe('ORD-2026-000001'));
    expect(calledUrls(spy)).toEqual(['http://api.test.invalid:5080/api/v1/orders?page=2&pageSize=10']);
  });
});

describe('useOrderQuery', () => {
  it('returns the order detail', async () => {
    mockFetchRoutes({ '/api/v1/orders/o-1': () => jsonResponse(200, makeOrderDetail()) });

    const { result } = await renderHook(() => useOrderQuery('o-1'), { wrapper: queryWrapper(createTestQueryClient()) });

    await waitFor(() => expect(result.current.data?.items[0].sku).toBe('TEE-BLK-M'));
  });

  it('surfaces a 403 as an ApiError with the status', async () => {
    mockFetchRoutes({ '/api/v1/orders/o-2': () => problemResponse(403, { title: 'Forbidden' }) });

    const { result } = await renderHook(() => useOrderQuery('o-2'), { wrapper: queryWrapper(createTestQueryClient()) });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBeInstanceOf(ApiError);
    expect((result.current.error as ApiError).status).toBe(403);
  });
});
