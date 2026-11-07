import { fireEvent, screen } from 'expo-router/testing-library';

import OrderDetailScreen from '@/app/orders/[id]';
import { resetSession } from '@/features/auth/session';
import { jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { makeOrderDetail } from '@/tests/order-fixtures';
import { renderRouterWithQuery } from '@/tests/render-with-query';
import { signInAs } from '@/tests/session';

jest.mock('expo-secure-store');

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const render = () => renderRouterWithQuery({ 'orders/[id]': OrderDetailScreen }, { initialUrl: '/orders/o-1' });

describe('OrderDetailView', () => {
  it('shows items, totals, shipping method and address', async () => {
    await signInAs('Customer');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeOrderDetail()));

    await render();

    expect(await screen.findByText('ORD-2026-000001')).toBeOnTheScreen();
    expect(screen.getByText('Black tee, M × 2')).toBeOnTheScreen();
    expect(screen.getByText('$40.00')).toBeOnTheScreen();
    expect(screen.getByText('Shipping $5.00')).toBeOnTheScreen();
    expect(screen.getByText('Total $45.00')).toBeOnTheScreen();
    expect(screen.getByText('Standard shipping')).toBeOnTheScreen();
    expect(screen.getByText('1 Example Street')).toBeOnTheScreen();
    expect(screen.getByText('Exampleville, 12345')).toBeOnTheScreen();
  });

  it('shows the timeline in the order the server sends it', async () => {
    await signInAs('Customer');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeOrderDetail()));

    await render();
    await screen.findByText('ORD-2026-000001');

    const labels = screen
      .getAllByText(/^(Awaiting payment|Paid|Processing|Shipped)$/)
      .map((node) => node.props.children as string);
    // The header status line comes first ("Shipped"), then the timeline rows in sequence.
    expect(labels.slice(1)).toEqual(['Awaiting payment', 'Paid', 'Processing', 'Shipped']);
  });

  it('shows the carrier, the tracking number and a tracking link for a shipped order', async () => {
    await signInAs('Customer');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeOrderDetail()));

    await render();

    expect(await screen.findByText('Example Post · AB 12/3')).toBeOnTheScreen();
    expect(screen.getByRole('link', { name: 'Track shipment' })).toBeOnTheScreen();
  });

  it('shows no tracking section before the order ships', async () => {
    await signInAs('Customer');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeOrderDetail({ status: 'Paid', shipments: [] })));

    await render();
    await screen.findByText('ORD-2026-000001');

    expect(screen.queryByRole('link', { name: 'Track shipment' })).toBeNull();
    expect(screen.queryByRole('header', { name: 'Tracking' })).toBeNull();
  });

  it('shows an error with Try again for a server error, and retry loads the order', async () => {
    await signInAs('Customer');
    let calls = 0;
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => {
      calls += 1;
      return calls === 1 ? problemResponse(500, { title: 'Server error' }) : jsonResponse(200, makeOrderDetail());
    });

    await render();
    await fireEvent.press(await screen.findByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('ORD-2026-000001')).toBeOnTheScreen();
  });

  it('shows "could not find" with no retry for another customer\'s order (403)', async () => {
    await signInAs('Customer');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => problemResponse(403, { title: 'Forbidden' }));

    await render();

    expect(await screen.findByText('We could not find that order.')).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Try again' })).toBeNull();
  });
});

// F12 (part, Slice 4): the detail shows the payment status of the latest attempt.
describe('OrderDetailView payment status', () => {
  it('shows the payment status next to the order status', async () => {
    await signInAs('Customer');
    jest
      .spyOn(globalThis, 'fetch')
      .mockImplementation(async () => jsonResponse(200, makeOrderDetail({ status: 'PendingPayment', paymentStatus: 'Failed' })));

    await render();

    expect(await screen.findByText('Payment: Payment failed')).toBeOnTheScreen();
  });

  it('shows no payment line when the order has no payment attempt', async () => {
    await signInAs('Customer');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeOrderDetail({ paymentStatus: null })));

    await render();
    await screen.findByText('ORD-2026-000001');

    expect(screen.queryByText(/^Payment:/)).toBeNull();
  });
});
