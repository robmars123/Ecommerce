import { QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import { fireEvent, renderRouter, screen, waitFor } from 'expo-router/testing-library';
import { Text } from 'react-native';

import { endSession, resetSession } from '@/features/auth/session';
import { useGuestCartStore } from '@/features/cart/guest-cart-store';
import { jsonResponse } from '@/tests/fetch-mock';
import { makeDetail, makeItem, makeVariant } from '@/tests/fixtures';
import { createTestQueryClient } from '@/tests/render-with-query';

import type { ProductDetail, ProductListItem } from '../types';
import { ProductCard } from './ProductCard';

jest.mock('expo-secure-store');

function routes(item = makeItem()) {
  const client = createTestQueryClient();
  return {
    _layout: () => (
      <QueryClientProvider client={client}>
        <Stack screenOptions={{ headerShown: false }} />
      </QueryClientProvider>
    ),
    index: () => <ProductCard item={item} />,
    'products/[slug]': () => <Text>Detail screen</Text>,
  };
}

beforeEach(async () => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
  useGuestCartStore.setState({ lines: [], mergeKey: null });
  await endSession();
});

afterEach(() => {
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const addButton = (name = 'Blue Shirt') => screen.getByRole('button', { name: `Add to Cart: ${name}` });
const mockDetail = (detail: ProductDetail) =>
  jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, detail));
const render = (item: ProductListItem = makeItem()) => renderRouter(routes(item), { initialUrl: '/' });

describe('ProductCard', () => {
  it('is a link named by the product name', async () => {
    await render();

    expect(screen.getByRole('link', { name: 'Blue Shirt' })).toBeOnTheScreen();
  });

  it('shows price, category, a labelled image placeholder and no badge for Unknown stock', async () => {
    await render();

    expect(screen.getByText('$24.50')).toBeOnTheScreen();
    expect(screen.getByText('Tops')).toBeOnTheScreen();
    expect(screen.getByLabelText('Blue shirt, front')).toBeOnTheScreen();
    expect(screen.queryByText(/stock/i)).toBeNull();
  });

  it('shows the stock badge and compare-at price when present', async () => {
    await render(makeItem({ stockStatus: 'Low', compareAtPrice: { amount: '30.00', currency: 'USD' } }));

    expect(screen.getByText('Low stock')).toBeOnTheScreen();
    expect(screen.getByLabelText('Was $30.00')).toBeOnTheScreen();
  });

  it('labels the image placeholder with the product name when there is no image', async () => {
    await render(makeItem({ primaryImage: null }));

    expect(screen.getAllByLabelText('Blue Shirt').length).toBeGreaterThanOrEqual(1);
  });

  it('navigates to /products/{slug} when the card body is tapped', async () => {
    await render();

    await fireEvent.press(screen.getByRole('link', { name: 'Blue Shirt' }));

    expect(screen).toHavePathname('/products/blue-shirt');
    expect(screen.getByText('Detail screen')).toBeOnTheScreen();
  });
});

describe('ProductCard content', () => {
  it('keeps the category text as data (styling upper-cases it) and offers no rating', async () => {
    await render();

    expect(screen.getByText('Tops')).toBeOnTheScreen();
    expect(screen.queryByText(/rating|review|★/i)).toBeNull();
    expect(screen.queryByText('Select options')).toBeNull();
  });
});

describe('ProductCard Add to Cart', () => {
  it('a product with one variant is added once with quantity 1, from the server detail', async () => {
    const spy = mockDetail(makeDetail());
    await render();

    await fireEvent.press(addButton());

    expect(await screen.findByText('Added to cart')).toBeOnTheScreen();
    expect(spy).toHaveBeenCalledTimes(1);
    expect(String(spy.mock.calls[0][0])).toBe('http://api.test.invalid:5080/api/v1/products/blue-shirt');
    expect(useGuestCartStore.getState().lines.map((l) => [l.variantId, l.quantity])).toEqual([['v-1', 1]]);
  });

  it('a product with several variants opens a picker; the chosen variant and quantity are added, sold-out ones are disabled', async () => {
    mockDetail(
      makeDetail({
        variants: [
          makeVariant({ id: 'v-1', name: 'Medium' }),
          makeVariant({ id: 'v-2', name: 'Large', price: { amount: '26.00', currency: 'USD' } }),
          makeVariant({ id: 'v-3', name: 'Small', stockStatus: 'Out' }),
        ],
      }),
    );
    await render();

    await fireEvent.press(addButton());

    expect(await screen.findByRole('button', { name: 'Large' })).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Small' })).toBeDisabled();
    expect(useGuestCartStore.getState().lines).toEqual([]);
    await fireEvent.press(screen.getByRole('button', { name: 'Large' }));
    expect(screen.getAllByText('$26.00').length).toBeGreaterThanOrEqual(1);
    await fireEvent.press(screen.getByRole('button', { name: 'Increase quantity' }));
    await fireEvent.press(screen.getByRole('button', { name: 'Increase quantity' }));
    await fireEvent.press(screen.getByRole('button', { name: 'Add to cart' }));

    expect(await screen.findByText('Added to cart')).toBeOnTheScreen();
    expect(useGuestCartStore.getState().lines.map((l) => [l.variantId, l.quantity])).toEqual([['v-2', 3]]);
  });

  it('an Out card has a disabled, labelled button and fetches nothing', async () => {
    const spy = jest.spyOn(globalThis, 'fetch');
    await render(makeItem({ stockStatus: 'Out' }));

    const button = screen.getByRole('button', { name: 'Add to Cart: Blue Shirt is out of stock' });
    await fireEvent.press(button);

    expect(button).toBeDisabled();
    expect(spy).not.toHaveBeenCalled();
  });

  it('while the detail loads the button is disabled, so a double tap adds only once', async () => {
    let release: (r: Response) => void = () => undefined;
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(() => new Promise<Response>((resolve) => (release = resolve)));
    await render();

    await fireEvent.press(addButton());
    await fireEvent.press(addButton());

    await waitFor(() => expect(addButton()).toBeDisabled());
    expect(spy).toHaveBeenCalledTimes(1);
    release(jsonResponse(200, makeDetail()));
    expect(await screen.findByText('Added to cart')).toBeOnTheScreen();
    expect(useGuestCartStore.getState().lines.map((l) => l.quantity)).toEqual([1]);
  });

  it('shows a plain message when the detail cannot be loaded and adds nothing', async () => {
    jest.spyOn(globalThis, 'fetch').mockRejectedValue(new TypeError('Network request failed'));
    await render();

    await fireEvent.press(addButton());

    expect(await screen.findByRole('alert')).toBeOnTheScreen();
    expect(useGuestCartStore.getState().lines).toEqual([]);
    expect(addButton()).toBeEnabled();
  });
});
