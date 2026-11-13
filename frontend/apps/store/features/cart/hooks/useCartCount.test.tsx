import { act, renderHook, waitFor } from '@testing-library/react-native';

import { endSession, resetSession } from '@/features/auth/session';
import { queryKeys } from '@/lib/query-keys';
import { makeCart, makeCartLine } from '@/tests/cart-fixtures';
import { jsonResponse } from '@/tests/fetch-mock';
import { createTestQueryClient, queryWrapper } from '@/tests/render-with-query';
import { signInAs } from '@/tests/session';

import { useGuestCartStore } from '../guest-cart-store';
import { useCartCount } from './useCartCount';

jest.mock('expo-secure-store');

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  useGuestCartStore.setState({ lines: [] });
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const twoLines = () => makeCart([makeCartLine({ variantId: 'v-1', quantity: 2 }), makeCartLine({ variantId: 'v-2', quantity: 1 })]);

describe('useCartCount', () => {
  it('T8: signed in, is null until the cart loads, then the sum of quantities', async () => {
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, twoLines()));
    await signInAs('Customer');

    const { result } = await renderHook(() => useCartCount(), { wrapper: queryWrapper(createTestQueryClient()) });

    expect(result.current).toBeNull();
    await waitFor(() => expect(result.current).toBe(3));
  });

  it('T9: signed in, stays null when the cart request fails', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => new Response('boom', { status: 500 }));
    await signInAs('Customer');

    const { result } = await renderHook(() => useCartCount(), { wrapper: queryWrapper(createTestQueryClient()) });

    await waitFor(() => expect(spy).toHaveBeenCalled());
    await act(async () => {});
    expect(result.current).toBeNull();
  });

  it('T10: a guest gets the sum of the device lines and no cart request is made', async () => {
    const spy = jest.spyOn(globalThis, 'fetch');
    await endSession();
    useGuestCartStore.setState({
      lines: [
        {
          variantId: 'v1',
          productSlug: 'tee',
          quantity: 2,
          snapshot: { productName: 'Tee', variantName: 'M', price: { amount: '10.00', currency: 'USD' }, image: null },
        },
        {
          variantId: 'v2',
          productSlug: 'tee',
          quantity: 4,
          snapshot: { productName: 'Tee', variantName: 'L', price: { amount: '10.00', currency: 'USD' }, image: null },
        },
      ],
    });

    const { result } = await renderHook(() => useCartCount(), { wrapper: queryWrapper(createTestQueryClient()) });

    expect(result.current).toBe(6);
    expect(spy).not.toHaveBeenCalled();
  });

  it('T11: signed in, writing the shared cart key drives the count', async () => {
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, twoLines()));
    await signInAs('Customer');
    const client = createTestQueryClient();
    const { result } = await renderHook(() => useCartCount(), { wrapper: queryWrapper(client) });
    await waitFor(() => expect(result.current).toBe(3));

    await act(async () => client.setQueryData(queryKeys.cart, makeCart([])));

    await waitFor(() => expect(result.current).toBe(0));
  });
});
