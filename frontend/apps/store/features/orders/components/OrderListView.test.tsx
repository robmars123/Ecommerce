import { fireEvent, screen, waitFor } from 'expo-router/testing-library';

import OrderListScreen from '@/app/orders/index';
import { endSession, resetSession } from '@/features/auth/session';
import { calledUrls, jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { makeOrderPage, makeOrderSummary } from '@/tests/order-fixtures';
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

const LoginStub = () => null;

const render = () => renderRouterWithQuery({ 'orders/index': OrderListScreen, login: LoginStub }, { initialUrl: '/orders' });

describe('OrderListView', () => {
  it('shows loading, then each order with number, date, status, total and a link to the detail', async () => {
    await signInAs('Customer');
    jest
      .spyOn(globalThis, 'fetch')
      .mockImplementation(async () =>
        jsonResponse(
          200,
          makeOrderPage([makeOrderSummary(), makeOrderSummary({ id: 'o-2', orderNumber: 'ORD-2026-000002', status: 'Delivered' })]),
        ),
      );

    await render();

    expect(screen.getByLabelText('Loading')).toBeOnTheScreen();
    expect(await screen.findByRole('link', { name: /^ORD-2026-000001\b/ })).toBeOnTheScreen();
    expect(screen.getByRole('link', { name: /^ORD-2026-000002\b/ })).toBeOnTheScreen();
    expect(screen.getAllByRole('link')[0]).toHaveAccessibleName('ORD-2026-000001 Mar 4, 2026 Paid $57.50'); // O2
    expect(screen.getAllByText('Mar 4, 2026')).toHaveLength(2);
    expect(screen.getAllByText('$57.50').length).toBe(2);
    expect(screen.getByText('Delivered')).toBeOnTheScreen();
  });

  it('shows a tracking link only for an order that has a tracking URL', async () => {
    await signInAs('Customer');
    jest
      .spyOn(globalThis, 'fetch')
      .mockImplementation(async () =>
        jsonResponse(
          200,
          makeOrderPage([
            makeOrderSummary({ status: 'Shipped', trackingUrl: 'https://tracking.example.com/T-1' }),
            makeOrderSummary({ id: 'o-2', orderNumber: 'ORD-2026-000002' }),
          ]),
        ),
      );

    await render();

    await screen.findByRole('link', { name: /^ORD-2026-000001\b/ });
    expect(screen.getAllByRole('link', { name: 'Track' })).toHaveLength(1);
    expect(screen.getAllByRole('link')[0]).not.toHaveAccessibleName(/Track/); // O2
  });

  it('never shows a placeholder word as a tracking number when the URL is not https', async () => {
    await signInAs('Customer');
    jest
      .spyOn(globalThis, 'fetch')
      .mockImplementation(async () =>
        jsonResponse(200, makeOrderPage([makeOrderSummary({ status: 'Shipped', trackingUrl: 'http://tracking.example.com/T-1' })])),
      );

    await render();

    await screen.findByRole('link', { name: /^ORD-2026-000001\b/ });
    expect(screen.queryByText('Tracking')).toBeNull();
    expect(screen.queryByRole('link', { name: 'Track' })).toBeNull();
  });

  it('shows an empty state when the customer has no orders', async () => {
    await signInAs('Customer');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeOrderPage([])));

    await render();

    expect(await screen.findByText('You have no orders yet.')).toBeOnTheScreen();
  });

  it('shows an error with Try again, and the retry loads the orders', async () => {
    await signInAs('Customer');
    let calls = 0;
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => {
      calls += 1;
      return calls === 1 ? problemResponse(500, { title: 'Server error' }) : jsonResponse(200, makeOrderPage([makeOrderSummary()]));
    });

    await render();
    await fireEvent.press(await screen.findByRole('button', { name: 'Try again' }));

    expect(await screen.findByRole('link', { name: /^ORD-2026-000001\b/ })).toBeOnTheScreen();
    expect(calledUrls(spy)).toHaveLength(2);
  });

  it('asks for the next page from the server', async () => {
    await signInAs('Customer');
    const spy = jest
      .spyOn(globalThis, 'fetch')
      .mockImplementation(async () => jsonResponse(200, makeOrderPage([makeOrderSummary()], { totalCount: 12, totalPages: 2 })));

    await render();
    await fireEvent.press(await screen.findByRole('button', { name: 'Next' }));

    await screen.findByRole('link', { name: /^ORD-2026-000001\b/ });
    expect(calledUrls(spy).some((u) => u.includes('page=2'))).toBe(true);
  });

  it('sends a signed-out visitor to the sign-in dialog and fetches nothing', async () => {
    const spy = jest.spyOn(globalThis, 'fetch');

    await render();
    await endSession();

    await waitFor(() => expect(screen).toHavePathname('/login'));
    expect(screen).toHaveSearchParams({ returnTo: '/orders' });
    expect(spy).not.toHaveBeenCalled();
  });
});
