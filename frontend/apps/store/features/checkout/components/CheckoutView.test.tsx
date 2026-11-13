import { fireEvent, screen, waitFor } from 'expo-router/testing-library';
import { Text } from 'react-native';

import CheckoutScreen from '@/app/checkout';
import { endSession, resetSession } from '@/features/auth/session';
import type { OrderStatus } from '@/features/orders/types';
import { makeCart, makeCartLine, usd } from '@/tests/cart-fixtures';
import { jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { renderedPaymentForms } from '@/tests/mock-payment-form';
import { makeOrderDetail } from '@/tests/order-fixtures';
import { queryKeys } from '@/lib/query-keys';
import { createTestQueryClient, renderRouterWithQuery } from '@/tests/render-with-query';
import { signInAs } from '@/tests/session';

import type { CheckoutResponse, ShippingMethodsResponse } from '../types';

jest.mock('expo-secure-store');

// The real form needs the provider SDK; the screen only hands it a secret and listens for its result (F11).
jest.mock('@/lib/platform', () => ({
  ...jest.requireActual('@/lib/platform'),
  PaymentForm: jest.requireActual('@/tests/mock-payment-form').MockPaymentForm,
}));

const BASE = 'http://api.test.invalid:5080';
const methods: ShippingMethodsResponse = {
  items: [
    { code: 'standard', name: 'Standard', price: usd('5.00'), estimatedDays: 5 },
    { code: 'express', name: 'Express', price: usd('12.00'), estimatedDays: 2 },
  ],
};

function placed(overrides: Partial<CheckoutResponse> = {}): CheckoutResponse {
  const detail = makeOrderDetail({ status: 'PendingPayment' });
  return {
    order: {
      id: 'o-1',
      orderNumber: detail.orderNumber,
      status: 'PendingPayment',
      items: detail.items,
      subtotal: usd('49.00'),
      discountTotal: usd('0.00'),
      shippingTotal: usd('5.00'),
      taxTotal: usd('0.60'),
      grandTotal: usd('54.60'),
      shippingMethod: { code: 'standard', name: 'Standard' },
      shippingAddress: detail.shippingAddress,
    },
    clientSecret: 'cs_1',
    ...overrides,
  };
}

type Call = { method: string; path: string; headers: Record<string, string>; body: unknown };
type Handler = (call: Call) => Response | Promise<Response>;

/** Exact method + path routes. Every request is recorded; an unmocked one answers 599 so the test fails loudly. */
function mockApi(handlers: Record<string, Handler>) {
  const calls: Call[] = [];
  jest.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const call: Call = {
      method: init?.method ?? 'GET',
      path: new URL(String(input)).pathname,
      headers: (init?.headers ?? {}) as Record<string, string>,
      body: typeof init?.body === 'string' ? JSON.parse(init.body) : undefined,
    };
    calls.push(call);
    const handler = handlers[`${call.method} ${call.path}`];
    return handler ? handler(call) : new Response(`not mocked: ${call.method} ${call.path}`, { status: 599 });
  });
  return {
    calls,
    to: (method: string, path: string) => calls.filter((c) => c.method === method && c.path === path),
  };
}

const baseHandlers = (): Record<string, Handler> => ({
  'GET /api/v1/cart': () => jsonResponse(200, makeCart()),
  'GET /api/v1/shipping-methods': () => jsonResponse(200, methods),
});

const render = (initialUrl = '/checkout') =>
  renderRouterWithQuery(
    {
      checkout: CheckoutScreen,
      'orders/[id]': () => <Text>Order page</Text>,
      products: () => <Text>Products page</Text>,
      cart: () => <Text>Cart page</Text>,
      login: () => <Text>Login page</Text>,
    },
    { initialUrl },
  );

async function fillAddress() {
  await fireEvent.changeText(screen.getByLabelText('Full name'), 'Test Buyer');
  await fireEvent.changeText(screen.getByLabelText('Address line 1'), '1 Example Street');
  await fireEvent.changeText(screen.getByLabelText('City'), 'Exampleville');
  await fireEvent.changeText(screen.getByLabelText('Postal code'), '12345');
  await fireEvent.changeText(screen.getByLabelText('Country (2-letter code, such as US)'), 'US');
}

const placeOrderButton = () => screen.getByRole('button', { name: 'Place order' });

async function placeOrder() {
  await screen.findByText('Shipping address');
  await fillAddress();
  await fireEvent.press(placeOrderButton());
}

beforeEach(async () => {
  process.env.EXPO_PUBLIC_API_BASE_URL = BASE;
  renderedPaymentForms.length = 0;
  await signInAs('Customer');
});

afterEach(() => {
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

describe('CheckoutView: details', () => {
  it('F10 lists the methods from the server and shows an estimate until the order exists', async () => {
    mockApi(baseHandlers());

    await render();

    expect(await screen.findByRole('radio', { name: 'Standard, $5.00, about 5 days' })).toBeOnTheScreen();
    expect(screen.getByRole('radio', { name: 'Express, $12.00, about 2 days' })).toBeOnTheScreen();
    expect(screen.getByText('Estimated total $54.00')).toBeOnTheScreen();
    expect(screen.getByText(/Tax is calculated at checkout/)).toBeOnTheScreen();

    await fireEvent.press(screen.getByRole('radio', { name: 'Express, $12.00, about 2 days' }));

    expect(screen.getByText('Estimated total $61.00')).toBeOnTheScreen();
  });

  it('F10 shows each server field error under its field', async () => {
    mockApi({
      ...baseHandlers(),
      'POST /api/v1/checkout': () =>
        problemResponse(400, {
          title: 'One or more validation errors occurred.',
          errors: {
            'shippingAddress.city': ['Enter a city of 1 to 100 characters.'],
            'shippingAddress.country': ['We do not ship to this country.'],
          },
        }),
    });
    await render();

    await placeOrder();

    expect(await screen.findByText('Enter a city of 1 to 100 characters.')).toBeOnTheScreen();
    expect(screen.getByText('We do not ship to this country.')).toBeOnTheScreen();
    expect(screen.queryByText('Payment form cs_1')).toBeNull();
  });

  it('F6 sends exactly an address and a method code, then shows the total from the server reply', async () => {
    const api = mockApi({
      ...baseHandlers(),
      'POST /api/v1/checkout': () => jsonResponse(200, placed()),
      'GET /api/v1/orders/o-1': () => jsonResponse(200, makeOrderDetail({ status: 'PendingPayment' })),
    });
    await render();
    expect(await screen.findByText('Estimated total $54.00')).toBeOnTheScreen();

    await placeOrder();

    expect(await screen.findByText('Total $54.60')).toBeOnTheScreen();
    expect(screen.queryByText(/Estimated total/)).toBeNull();
    const sent = api.to('POST', '/api/v1/checkout')[0].body as Record<string, unknown>;
    expect(Object.keys(sent).sort()).toEqual(['shippingAddress', 'shippingMethodCode']);
    expect(sent.shippingMethodCode).toBe('standard');
    expect(sent.shippingAddress).toEqual({
      fullName: 'Test Buyer',
      line1: '1 Example Street',
      city: 'Exampleville',
      postalCode: '12345',
      country: 'US',
    });
    expect(JSON.stringify(sent)).not.toMatch(/price|total|tax|status|amount/i);
  });

  it('F5 disables the button while the request is pending and a second tap sends nothing', async () => {
    let release: (r: Response) => void = () => undefined;
    const api = mockApi({
      ...baseHandlers(),
      'POST /api/v1/checkout': () => new Promise<Response>((resolve) => (release = resolve)),
      'GET /api/v1/orders/o-1': () => jsonResponse(200, makeOrderDetail({ status: 'PendingPayment' })),
    });
    await render();

    await placeOrder();
    await fireEvent.press(placeOrderButton());

    await waitFor(() => expect(placeOrderButton()).toBeDisabled());
    expect(api.to('POST', '/api/v1/checkout')).toHaveLength(1);

    release(jsonResponse(200, placed()));
    expect(await screen.findByText('Payment form cs_1')).toBeOnTheScreen();
  });

  it('F4 a network failure then a second tap sends the same key; after a 409 fix the next attempt has a new key', async () => {
    let attempt = 0;
    const api = mockApi({
      ...baseHandlers(),
      'POST /api/v1/checkout': () => {
        attempt += 1;
        if (attempt === 1) throw new TypeError('Network request failed');
        if (attempt === 2) return problemResponse(409, { type: 'cart-empty', title: 'Your cart is empty' });
        return jsonResponse(200, placed());
      },
      'GET /api/v1/orders/o-1': () => jsonResponse(200, makeOrderDetail({ status: 'PendingPayment' })),
    });
    await render();
    await placeOrder();
    await screen.findByText("Can't reach the store. Check your connection and try again.");

    await fireEvent.press(placeOrderButton());
    await screen.findByText('Your cart is empty.');
    await fireEvent.press(placeOrderButton());
    await screen.findByText('Payment form cs_1');

    const [first, second, third] = api.to('POST', '/api/v1/checkout').map((c) => c.headers['Idempotency-Key']);
    expect(second).toBe(first);
    expect(third).not.toBe(second);
  });

  it('F4 after a network failure or a 503 the fields are locked and the next tap resends the identical body with the same key', async () => {
    let attempt = 0;
    const api = mockApi({
      ...baseHandlers(),
      'POST /api/v1/checkout': () => {
        attempt += 1;
        if (attempt === 1) throw new TypeError('Network request failed');
        if (attempt === 2) return problemResponse(503, { type: 'payment-provider-unavailable', orderId: 'o-1' });
        return jsonResponse(200, placed());
      },
      'GET /api/v1/orders/o-1': () => jsonResponse(200, makeOrderDetail({ status: 'PendingPayment' })),
    });
    await render();
    await placeOrder();
    await screen.findByText("Can't reach the store. Check your connection and try again.");

    expect(screen.getByLabelText('City').props.editable).toBe(false);
    expect(screen.getByLabelText('Postal code').props.editable).toBe(false);
    await fireEvent.changeText(screen.getByLabelText('City'), 'Elsewhere');
    await fireEvent.press(placeOrderButton());
    await screen.findByText('Payment is temporarily unavailable. Please try again.');
    expect(screen.getByLabelText('City').props.editable).toBe(false);
    await fireEvent.press(placeOrderButton());
    await screen.findByText('Payment form cs_1');

    const calls = api.to('POST', '/api/v1/checkout');
    expect(calls).toHaveLength(3);
    expect(calls[1].body).toEqual(calls[0].body);
    expect(calls[2].body).toEqual(calls[0].body);
    expect(calls[1].headers['Idempotency-Key']).toBe(calls[0].headers['Idempotency-Key']);
    expect(calls[2].headers['Idempotency-Key']).toBe(calls[0].headers['Idempotency-Key']);
  });

  it('F4 a definitive answer unlocks the fields again', async () => {
    mockApi({
      ...baseHandlers(),
      'POST /api/v1/checkout': () => problemResponse(409, { type: 'cart-empty', title: 'Your cart is empty' }),
    });
    await render();

    await placeOrder();
    await screen.findByText('Your cart is empty.');

    expect(screen.getByLabelText('City').props.editable).toBe(true);
  });

  it('shows each rejected line by name, and "Accept new prices" re-prices the line at the same quantity and tries again with a new key', async () => {
    let attempt = 0;
    const api = mockApi({
      ...baseHandlers(),
      'POST /api/v1/checkout': () => {
        attempt += 1;
        return attempt === 1
          ? problemResponse(409, {
              type: 'checkout-lines-rejected',
              title: 'Some items cannot be ordered',
              lines: [{ variantId: 'v-1', reason: 'PriceChanged' }],
            })
          : jsonResponse(200, placed());
      },
      'PUT /api/v1/cart/items/v-1': () =>
        jsonResponse(200, makeCart([makeCartLine({ unitPrice: usd('25.00'), lineTotal: usd('50.00') })], '50.00')),
      'GET /api/v1/orders/o-1': () => jsonResponse(200, makeOrderDetail({ status: 'PendingPayment' })),
    });
    await render();
    await placeOrder();

    expect(await screen.findByText('Blue Shirt (Medium): the price changed.')).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: 'Accept new prices' }));

    expect(await screen.findByText('Payment form cs_1')).toBeOnTheScreen();
    expect(api.to('PUT', '/api/v1/cart/items/v-1')[0].body).toEqual({ quantity: 2 });
    const [first, second] = api.to('POST', '/api/v1/checkout').map((c) => c.headers['Idempotency-Key']);
    expect(second).not.toBe(first);
  });

  it('a sold-out line is named with a link back to the cart', async () => {
    mockApi({
      ...baseHandlers(),
      'POST /api/v1/checkout': () =>
        problemResponse(409, {
          type: 'checkout-lines-rejected',
          title: 'x',
          lines: [{ variantId: 'v-1', reason: 'OutOfStock', available: 1 }],
        }),
    });
    await render();

    await placeOrder();

    expect(await screen.findByText('Blue Shirt (Medium): only 1 left.')).toBeOnTheScreen();
    expect(screen.getByRole('link', { name: 'Edit your cart' })).toBeOnTheScreen();
  });

  it('a provider outage says so and the next tap repeats the same key', async () => {
    let attempt = 0;
    const api = mockApi({
      ...baseHandlers(),
      'POST /api/v1/checkout': () => {
        attempt += 1;
        return attempt === 1 ? problemResponse(503, { type: 'payment-provider-unavailable', orderId: 'o-1' }) : jsonResponse(200, placed());
      },
      'GET /api/v1/orders/o-1': () => jsonResponse(200, makeOrderDetail({ status: 'PendingPayment' })),
    });
    await render();
    await placeOrder();
    await screen.findByText('Payment is temporarily unavailable. Please try again.');

    await fireEvent.press(placeOrderButton());
    await screen.findByText('Payment form cs_1');

    const [first, second] = api.to('POST', '/api/v1/checkout').map((c) => c.headers['Idempotency-Key']);
    expect(second).toBe(first);
  });
});

describe('CheckoutView: payment', () => {
  const afterPlacing = (extra: Record<string, Handler> = {}) =>
    mockApi({
      ...baseHandlers(),
      'POST /api/v1/checkout': () => jsonResponse(200, placed()),
      'GET /api/v1/orders/o-1': () => jsonResponse(200, makeOrderDetail({ status: 'PendingPayment' })),
      ...extra,
    });

  it('F11 hands the client secret to the payment form and puts the order id in the URL', async () => {
    afterPlacing();
    await render();

    await placeOrder();

    expect(await screen.findByText('Payment form cs_1')).toBeOnTheScreen();
    expect(renderedPaymentForms[renderedPaymentForms.length - 1].returnPath).toBe('/checkout?orderId=o-1');
    expect(screen).toHavePathname('/checkout');
    expect(screen).toHaveSearchParams({ orderId: 'o-1' });
  });

  it('F5 says it is confirming, never paid, until the polled order is Paid', async () => {
    let reads = 0;
    afterPlacing({
      'GET /api/v1/orders/o-1': () => {
        reads += 1;
        return jsonResponse(200, makeOrderDetail({ status: (reads < 3 ? 'PendingPayment' : 'Paid') as OrderStatus }));
      },
    });
    await render();
    await placeOrder();

    await fireEvent.press(await screen.findByRole('button', { name: 'Mock pay' }));

    expect(await screen.findByText('Awaiting payment confirmation')).toBeOnTheScreen();
    expect(screen.queryByText('Payment received')).toBeNull();
    expect(await screen.findByText('Payment received', {}, { timeout: 8000 })).toBeOnTheScreen();
    expect(screen.getByRole('link', { name: 'View your order' })).toBeOnTheScreen();
  }, 15000);

  it('a "processing" result also waits for the server', async () => {
    afterPlacing();
    await render();
    await placeOrder();

    await fireEvent.press(await screen.findByRole('button', { name: 'Mock processing' }));

    expect(await screen.findByText('Awaiting payment confirmation')).toBeOnTheScreen();
  });

  it('F11 a declined card offers Retry payment; one key per retry, reused after a network failure', async () => {
    let retries = 0;
    const api = afterPlacing({
      'POST /api/v1/orders/o-1/retry-payment': () => {
        retries += 1;
        if (retries === 1) throw new TypeError('Network request failed');
        return jsonResponse(200, { orderId: 'o-1', attempt: 1 + retries, clientSecret: `cs_${retries + 1}` });
      },
    });
    await render();
    await placeOrder();

    await fireEvent.press(await screen.findByRole('button', { name: 'Mock decline' }));
    expect(await screen.findByText('Your card was declined.')).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: 'Retry payment' }));
    await screen.findByText("Can't reach the store. Check your connection and try again.");
    await fireEvent.press(screen.getByRole('button', { name: 'Retry payment' }));
    expect(await screen.findByText('Payment form cs_3')).toBeOnTheScreen();

    await fireEvent.press(screen.getByRole('button', { name: 'Mock decline' }));
    await fireEvent.press(await screen.findByRole('button', { name: 'Retry payment' }));
    expect(await screen.findByText('Payment form cs_4')).toBeOnTheScreen();

    const keys = api.to('POST', '/api/v1/orders/o-1/retry-payment').map((c) => c.headers['Idempotency-Key']);
    expect(keys).toHaveLength(3);
    expect(keys[1]).toBe(keys[0]);
    expect(keys[2]).not.toBe(keys[1]);
  });

  it('F11 an expired reservation says so and "Check out again" returns to the details with no order id', async () => {
    afterPlacing({
      'POST /api/v1/orders/o-1/retry-payment': () =>
        problemResponse(409, { type: 'reservation-expired', title: 'The reservation has expired; check out again' }),
    });
    await render();
    await placeOrder();
    await fireEvent.press(await screen.findByRole('button', { name: 'Mock decline' }));
    await fireEvent.press(await screen.findByRole('button', { name: 'Retry payment' }));

    expect(await screen.findByText('Your reservation has expired.')).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: 'Check out again' }));

    expect(await screen.findByText('Shipping address')).toBeOnTheScreen();
    expect(placeOrderButton()).toBeOnTheScreen();
    expect(screen).not.toHaveSearchParams({ orderId: 'o-1' });
  });

  it('a refresh at /checkout?orderId resumes that order with "Continue to payment" and never places a second order', async () => {
    const api = mockApi({
      ...baseHandlers(),
      'GET /api/v1/orders/o-1': () => jsonResponse(200, makeOrderDetail({ status: 'PendingPayment' })),
      'POST /api/v1/orders/o-1/retry-payment': () => jsonResponse(200, { orderId: 'o-1', attempt: 2, clientSecret: 'cs_resumed' }),
    });
    await render('/checkout?orderId=o-1');

    expect(await screen.findByText('Total $45.00')).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: 'Continue to payment' }));

    expect(await screen.findByText('Payment form cs_resumed')).toBeOnTheScreen();
    expect(api.to('POST', '/api/v1/checkout')).toHaveLength(0);
    expect(screen.queryByRole('button', { name: 'Place order' })).toBeNull();
  });

  it('a returning redirect with redirect_status=succeeded waits for the server instead of showing the form', async () => {
    mockApi({ ...baseHandlers(), 'GET /api/v1/orders/o-1': () => jsonResponse(200, makeOrderDetail({ status: 'PendingPayment' })) });

    await render('/checkout?orderId=o-1&redirect_status=succeeded');

    expect(await screen.findByText('Awaiting payment confirmation')).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Continue to payment' })).toBeNull();
  });

  it('a cancelled order says so and offers Check out again', async () => {
    mockApi({ ...baseHandlers(), 'GET /api/v1/orders/o-1': () => jsonResponse(200, makeOrderDetail({ status: 'Cancelled' })) });

    await render('/checkout?orderId=o-1');

    expect(
      await screen.findByText(
        'This order was cancelled because the payment was not completed in time. If a payment went through, it is refunded automatically.',
      ),
    ).toBeOnTheScreen();
    expect(screen.queryByText(/Nothing was charged/)).toBeNull();
    expect(screen.getByRole('button', { name: 'Check out again' })).toBeOnTheScreen();
  });

  it('an already paid order shows "Payment received"', async () => {
    mockApi({ ...baseHandlers(), 'GET /api/v1/orders/o-1': () => jsonResponse(200, makeOrderDetail({ status: 'Paid' })) });

    await render('/checkout?orderId=o-1');

    expect(await screen.findByText('Payment received')).toBeOnTheScreen();
  });

  const renderWith = (client: ReturnType<typeof createTestQueryClient>) =>
    renderRouterWithQuery(
      { checkout: CheckoutScreen, 'orders/[id]': () => <Text>Order page</Text>, cart: () => <Text>Cart page</Text> },
      { initialUrl: '/checkout?orderId=o-1', client },
    );

  it('T14: once the server says the order is paid, the cart query is invalidated (the header badge refetches)', async () => {
    mockApi({ ...baseHandlers(), 'GET /api/v1/orders/o-1': () => jsonResponse(200, makeOrderDetail({ status: 'Paid' })) });
    const client = createTestQueryClient();
    client.setQueryData(queryKeys.cart, makeCart());

    await renderWith(client);

    expect(await screen.findByText('Payment received')).toBeOnTheScreen();
    await waitFor(() => expect(client.getQueryState(queryKeys.cart)?.isInvalidated).toBe(true));
  });

  it('T15: while the order is still PendingPayment the cart query is not invalidated', async () => {
    mockApi({ ...baseHandlers(), 'GET /api/v1/orders/o-1': () => jsonResponse(200, makeOrderDetail({ status: 'PendingPayment' })) });
    const client = createTestQueryClient();
    client.setQueryData(queryKeys.cart, makeCart());

    await renderWith(client);

    expect(await screen.findByRole('button', { name: 'Continue to payment' })).toBeOnTheScreen();
    expect(client.getQueryState(queryKeys.cart)?.isInvalidated).toBe(false);
  });
});

describe('CheckoutView: access', () => {
  it('a signed-out visitor is sent to sign in and no checkout request is made', async () => {
    await endSession();
    const spy = jest.spyOn(globalThis, 'fetch');

    await render();

    expect(await screen.findByText('Login page')).toBeOnTheScreen();
    expect(spy).not.toHaveBeenCalled();
  });

  it('an empty cart shows a link to browse and no form', async () => {
    mockApi({ ...baseHandlers(), 'GET /api/v1/cart': () => jsonResponse(200, makeCart([], '0.00')) });

    await render();

    expect(await screen.findByText('Your cart is empty.')).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Place order' })).toBeNull();
  });
});
