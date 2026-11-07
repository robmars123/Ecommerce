import { fireEvent, screen } from 'expo-router/testing-library';
import { Text } from 'react-native';

import { endSession, resetSession } from '@/features/auth/session';
import { jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { makeCart } from '@/tests/cart-fixtures';
import { makeDetail, makeVariant } from '@/tests/fixtures';
import { renderRouterWithQuery } from '@/tests/render-with-query';
import { signInAs } from '@/tests/session';

import { useGuestCartStore } from '../guest-cart-store';
import { AddToCartControl } from './AddToCartControl';

jest.mock('expo-secure-store');

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
  useGuestCartStore.setState({ lines: [], mergeKey: null });
});
afterEach(() => {
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const product = makeDetail();

const render = (variant = makeVariant({ id: 'v-1', stockStatus: 'InStock' })) =>
  renderRouterWithQuery(
    { index: () => <AddToCartControl product={product} variant={variant} />, cart: () => <Text>Cart screen</Text> },
    { initialUrl: '/' },
  );

describe('AddToCartControl', () => {
  it('signed in posts the variant and the quantity only, and offers a link to the cart', async () => {
    await signInAs('Customer');
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeCart()));

    await render();
    await fireEvent.press(screen.getByRole('button', { name: 'Increase quantity' }));
    await fireEvent.press(screen.getByRole('button', { name: 'Add to cart' }));

    expect(await screen.findByText('Added to cart')).toBeOnTheScreen();
    const [url, init] = spy.mock.calls[0];
    expect(String(url)).toBe('http://api.test.invalid:5080/api/v1/cart/items');
    expect(JSON.parse(String(init?.body))).toEqual({ variantId: 'v-1', quantity: 2 });
    await fireEvent.press(screen.getByRole('link', { name: 'View cart' }));
    expect(screen).toHavePathname('/cart');
  });

  it('shows the server message on a stock conflict and not "Added to cart"', async () => {
    await signInAs('Customer');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => problemResponse(409, { title: 'Not enough stock', available: 1 }));

    await render();
    await fireEvent.press(screen.getByRole('button', { name: 'Add to cart' }));

    expect(await screen.findByText('Only 1 available.')).toBeOnTheScreen();
    expect(screen.queryByText('Added to cart')).toBeNull();
  });

  it('signed out adds to the device cart with a snapshot and sends nothing', async () => {
    await endSession();
    const spy = jest.spyOn(globalThis, 'fetch');
    await render();

    await fireEvent.press(screen.getByRole('button', { name: 'Add to cart' }));

    expect(await screen.findByText('Added to cart')).toBeOnTheScreen();
    expect(useGuestCartStore.getState().lines).toMatchObject([
      { variantId: 'v-1', productSlug: 'blue-shirt', quantity: 1, snapshot: { productName: 'Blue Shirt', price: { amount: '24.50' } } },
    ]);
    expect(spy).not.toHaveBeenCalled();
  });

  it('is disabled for an out-of-stock variant', async () => {
    await render(makeVariant({ id: 'v-2', stockStatus: 'Out' }));

    expect(screen.getByRole('button', { name: 'Add to cart' })).toBeDisabled();
    expect(screen.getByText('This item is out of stock.')).toBeOnTheScreen();
  });

  it('keeps the quantity between 1 and 10', async () => {
    await render();

    await fireEvent.press(screen.getByRole('button', { name: 'Decrease quantity' }));
    expect(screen.getByLabelText('Quantity to add')).toHaveTextContent('1');
    for (let i = 0; i < 15; i += 1) {
      await fireEvent.press(screen.getByRole('button', { name: 'Increase quantity' }));
    }
    expect(screen.getByLabelText('Quantity to add')).toHaveTextContent('10');
  });
});
