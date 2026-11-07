import { fireEvent, screen, waitFor } from 'expo-router/testing-library';
import { Text } from 'react-native';

import CartScreen from '@/app/cart';
import { endSession, resetSession } from '@/features/auth/session';
import { jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { makeCart, makeCartLine, usd } from '@/tests/cart-fixtures';
import { renderRouterWithQuery } from '@/tests/render-with-query';
import { signInAs } from '@/tests/session';

import { useGuestCartStore } from '../guest-cart-store';

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

const render = () =>
  renderRouterWithQuery(
    { cart: CartScreen, login: () => <Text>Login screen</Text>, 'products/index': () => <Text>List screen</Text> },
    { initialUrl: '/cart' },
  );

function calls(spy: jest.SpyInstance, method: string) {
  return spy.mock.calls.filter((c) => c[1]?.method === method);
}

// F3: the server result wins; the screen shows the server strings as given.
describe('CartView (signed in)', () => {
  it('shows the server unit price, line total and subtotal exactly as sent, with each issue in words', async () => {
    await signInAs('Customer');
    const cart = makeCart(
      [
        makeCartLine({ unitPrice: usd('12.50'), lineTotal: usd('25.00'), issues: ['PriceChanged'] }),
        makeCartLine({ variantId: 'v-2', productName: 'Red Hat', variantName: 'One size', issues: ['ExceedsStock', 'Unavailable'] }),
      ],
      '73.50',
    );
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, cart));

    await render();

    expect(await screen.findByText('Blue Shirt')).toBeOnTheScreen();
    expect(screen.getByText('$12.50 each')).toBeOnTheScreen();
    expect(screen.getByText('Line total $25.00')).toBeOnTheScreen();
    expect(screen.getByText('Subtotal $73.50')).toBeOnTheScreen();
    expect(screen.getByText('The price changed since you added this.')).toBeOnTheScreen();
    expect(screen.getByText(/less in stock than you have/)).toBeOnTheScreen();
    expect(screen.getByText(/no longer available/)).toBeOnTheScreen();
  });

  it('shows an empty cart with a link back to browsing', async () => {
    await signInAs('Customer');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeCart([], '0.00')));

    await render();

    expect(await screen.findByText('Your cart is empty.')).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: 'Browse products' }));
    expect(screen).toHavePathname('/products');
  });

  it('replaces the line with the server reply after a quantity change: the old price is gone', async () => {
    await signInAs('Customer');
    const before = makeCart([makeCartLine({ quantity: 2 })]);
    const after = makeCart([makeCartLine({ quantity: 3, unitPrice: usd('26.00'), lineTotal: usd('78.00') })], '78.00');
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async (_url, init) =>
      jsonResponse(200, init?.method === 'PUT' ? after : before),
    );

    await render();
    await screen.findByText('Line total $49.00');
    await fireEvent.press(screen.getByRole('button', { name: 'Increase quantity of Blue Shirt - Medium' }));

    expect(await screen.findByText('Line total $78.00')).toBeOnTheScreen();
    expect(screen.queryByText('Line total $49.00')).toBeNull();
    expect(screen.queryByText('$24.50 each')).toBeNull();
    expect(screen.getByText('Subtotal $78.00')).toBeOnTheScreen();
    expect(JSON.parse(String(calls(spy, 'PUT')[0][1]?.body))).toEqual({ quantity: 3 });
  });

  it('on a 409 shows how many are available and refetches, so the old quantity is not kept', async () => {
    await signInAs('Customer');
    let current = makeCart([makeCartLine({ quantity: 2 })]);
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async (_url, init) =>
      init?.method === 'PUT' ? problemResponse(409, { title: 'Not enough stock', available: 2 }) : jsonResponse(200, current),
    );

    await render();
    await screen.findByText('Line total $49.00');
    current = makeCart([makeCartLine({ quantity: 2, issues: ['ExceedsStock'] })]);
    await fireEvent.press(screen.getByRole('button', { name: 'Increase quantity of Blue Shirt - Medium' }));

    expect(await screen.findByText('Only 2 available.')).toBeOnTheScreen();
    await waitFor(() => expect(screen.getByText(/less in stock than you have/)).toBeOnTheScreen());
    expect(calls(spy, 'GET').length).toBeGreaterThanOrEqual(2);
  });

  it('remove sends DELETE and refetches the cart', async () => {
    await signInAs('Customer');
    let current = makeCart([makeCartLine()]);
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async (_url, init) =>
      init?.method === 'DELETE' ? new Response(null, { status: 204 }) : jsonResponse(200, current),
    );

    await render();
    await screen.findByText('Blue Shirt');
    current = makeCart([], '0.00');
    await fireEvent.press(screen.getByRole('button', { name: 'Remove Blue Shirt - Medium' }));

    expect(await screen.findByText('Your cart is empty.')).toBeOnTheScreen();
    expect(calls(spy, 'DELETE')).toHaveLength(1);
  });
});

describe('CartView (signed out)', () => {
  it('shows the device cart with estimated prices, edits it locally and sends nothing to the server', async () => {
    await endSession();
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, {}));
    useGuestCartStore.getState().add(
      {
        variantId: 'v-1',
        productSlug: 'blue-shirt',
        snapshot: { productName: 'Blue Shirt', variantName: 'Medium', price: usd('24.50'), image: null },
      },
      2,
    );

    await render();

    expect(await screen.findByText('Estimated price $24.50 each')).toBeOnTheScreen();
    expect(screen.queryByText(/Line total|Subtotal/)).toBeNull();
    await fireEvent.press(screen.getByRole('button', { name: 'Increase quantity of Blue Shirt - Medium' }));
    expect(useGuestCartStore.getState().lines[0].quantity).toBe(3);
    await fireEvent.press(screen.getByRole('button', { name: 'Remove Blue Shirt - Medium' }));
    expect(await screen.findByText('Your cart is empty.')).toBeOnTheScreen();
    expect(spy).not.toHaveBeenCalled();
  });
});

describe('CartView checkout link (Slice 4)', () => {
  it('a signed-in cart with items links to checkout', async () => {
    await signInAs('Customer');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeCart()));

    await render();

    expect(await screen.findByRole('link', { name: 'Checkout' })).toBeOnTheScreen();
  });
});
