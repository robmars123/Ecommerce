import * as Clipboard from 'expo-clipboard';
import { act, fireEvent, screen } from 'expo-router/testing-library';

import OrderDetailScreen from '@/app/orders/[id]';
import { resetSession } from '@/features/auth/session';
import { jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { makeOrderDetail } from '@/tests/order-fixtures';
import { renderRouterWithQuery } from '@/tests/render-with-query';
import { signInAs } from '@/tests/session';

jest.mock('expo-secure-store');

const setString = jest.mocked(Clipboard.setStringAsync);

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
  setString.mockReset();
  setString.mockResolvedValue(true);
});

afterEach(() => {
  jest.useRealTimers();
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const render = () => renderRouterWithQuery({ 'orders/[id]': OrderDetailScreen }, { initialUrl: '/orders/o-1' });

async function renderLoaded() {
  await signInAs('Customer');
  jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeOrderDetail()));
  await render();
  await screen.findByRole('header', { name: 'Order ORD-2026-000001' });
}

describe('OrderDetailView copy order number', () => {
  it('shows a copy button and keeps the header group intact', async () => {
    await renderLoaded();

    expect(screen.getByRole('button', { name: 'Copy order number' })).toBeOnTheScreen();
    expect(screen.getByRole('header', { name: 'Order ORD-2026-000001' })).toBeOnTheScreen();
  });

  it('copies the order number once and confirms', async () => {
    await renderLoaded();

    await fireEvent.press(screen.getByRole('button', { name: 'Copy order number' }));

    expect(setString).toHaveBeenCalledTimes(1);
    expect(setString).toHaveBeenCalledWith('ORD-2026-000001');
    expect(await screen.findByRole('button', { name: 'Order number copied' })).toBeOnTheScreen();
    expect(screen.getByText('Copied')).toBeOnTheScreen();
  });

  it('reports failure when the copy resolves false', async () => {
    await renderLoaded();
    setString.mockResolvedValue(false);

    await fireEvent.press(screen.getByRole('button', { name: 'Copy order number' }));

    expect(await screen.findByRole('button', { name: "Couldn't copy order number" })).toBeOnTheScreen();
    expect(screen.getByText('Copy failed')).toBeOnTheScreen();
  });

  it('reports failure when the copy rejects', async () => {
    await renderLoaded();
    setString.mockRejectedValue(new Error('denied'));

    await fireEvent.press(screen.getByRole('button', { name: 'Copy order number' }));

    expect(await screen.findByRole('button', { name: "Couldn't copy order number" })).toBeOnTheScreen();
    expect(screen.getByText('Copy failed')).toBeOnTheScreen();
  });

  it('returns to the idle label after 2 seconds', async () => {
    await renderLoaded();
    jest.useFakeTimers();

    await fireEvent.press(screen.getByRole('button', { name: 'Copy order number' }));
    expect(await screen.findByRole('button', { name: 'Order number copied' })).toBeOnTheScreen();

    await act(async () => {
      jest.advanceTimersByTime(2000);
    });

    expect(screen.getByRole('button', { name: 'Copy order number' })).toBeOnTheScreen();
  });

  it('is not rendered when the order fails to load', async () => {
    await signInAs('Customer');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => problemResponse(404, { title: 'Not found', status: 404 }));

    await render();
    await screen.findByText('We could not find that order.');

    expect(screen.queryByRole('button', { name: 'Copy order number' })).toBeNull();
  });
});
