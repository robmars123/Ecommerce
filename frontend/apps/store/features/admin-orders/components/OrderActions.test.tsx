import { fireEvent, screen, waitFor } from 'expo-router/testing-library';

import { resetSession } from '@/features/auth/session';
import type { OrderStatus } from '@/features/orders/types';
import { makeAdminOrderDetail } from '@/tests/admin-order-fixtures';
import { jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { makeOrderDetail } from '@/tests/order-fixtures';
import { renderRouterWithQuery } from '@/tests/render-with-query';
import { signInAs } from '@/tests/session';

import { useAdminOrderQuery } from '../hooks/hooks';
import { OrderActions } from './OrderActions';

jest.mock('expo-secure-store');

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const detailWith = (status: OrderStatus) => makeAdminOrderDetail({ order: makeOrderDetail({ id: 'o-9', status }) });
const render = (status: OrderStatus) =>
  renderRouterWithQuery({ index: () => <OrderActions detail={detailWith(status)} /> }, { initialUrl: '/' });
const noContent = () => new Response(null, { status: 204 });

describe('OrderActions', () => {
  it.each([
    ['Paid', ['Process', 'Override to Processing']],
    ['Processing', ['Ship']],
    ['Shipped', ['Deliver', 'Override to Delivered']],
  ] as const)('shows the %s buttons to an Admin', async (status, names) => {
    await signInAs('Admin');
    await render(status);

    for (const name of names) {
      expect(screen.getByRole('button', { name })).toBeOnTheScreen();
    }
  });

  it('shows no buttons for a status with no action', async () => {
    await signInAs('Admin');
    await render('Delivered');

    expect(screen.queryByText('Fulfillment')).toBeNull();
  });

  it('shows Support no buttons', async () => {
    await signInAs('Support');
    await render('Paid');

    expect(screen.queryByRole('button', { name: 'Process' })).toBeNull();
  });

  it('posts process, disables the button while pending and sends no Idempotency-Key', async () => {
    await signInAs('Admin');
    let release: (r: Response) => void = () => undefined;
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(() => new Promise<Response>((resolve) => (release = resolve)));
    await render('Paid');

    await fireEvent.press(screen.getByRole('button', { name: 'Process' }));

    await waitFor(() => expect(screen.getByRole('button', { name: 'Process' })).toBeDisabled());
    const [url, init] = spy.mock.calls[0] as [string, RequestInit];
    expect(url).toBe('http://api.test.invalid:5080/api/v1/admin/orders/o-9/process');
    expect((init.headers as Record<string, string>)['Idempotency-Key']).toBeUndefined();
    release(noContent());
    await waitFor(() => expect(screen.getByRole('button', { name: 'Process' })).not.toBeDisabled());
  });

  it('blocks a ship with an empty carrier or a 65 character tracking number, then sends a valid one', async () => {
    await signInAs('Admin');
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => noContent());
    await render('Processing');
    await fireEvent.press(screen.getByRole('button', { name: 'Ship' }));

    await fireEvent.press(screen.getByRole('button', { name: 'Confirm ship' }));
    expect(screen.getByText('Enter a carrier code.')).toBeOnTheScreen();
    expect(screen.getByText('Enter a tracking number of 1 to 64 characters.')).toBeOnTheScreen();
    await fireEvent.changeText(screen.getByLabelText('Carrier code'), 'example-post');
    await fireEvent.changeText(screen.getByLabelText('Tracking number'), 'x'.repeat(65));
    await fireEvent.press(screen.getByRole('button', { name: 'Confirm ship' }));
    expect(screen.getByText('Enter a tracking number of 1 to 64 characters.')).toBeOnTheScreen();
    expect(spy).not.toHaveBeenCalled();

    await fireEvent.changeText(screen.getByLabelText('Tracking number'), 'T-123');
    await fireEvent.press(screen.getByRole('button', { name: 'Confirm ship' }));

    await waitFor(() => expect(spy).toHaveBeenCalledTimes(1));
    expect(JSON.parse((spy.mock.calls[0][1] as RequestInit).body as string)).toEqual({
      carrierCode: 'example-post',
      trackingNumber: 'T-123',
    });
  });

  it('blocks an override without a reason, then sends the target status with it', async () => {
    await signInAs('Admin');
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => noContent());
    await render('Shipped');
    await fireEvent.press(screen.getByRole('button', { name: 'Override to Delivered' }));

    await fireEvent.press(screen.getByRole('button', { name: 'Confirm override' }));
    expect(screen.getByText('Enter a reason of 1 to 500 characters.')).toBeOnTheScreen();
    expect(spy).not.toHaveBeenCalled();

    await fireEvent.changeText(screen.getByLabelText('Override reason'), 'Customer confirmed receipt');
    await fireEvent.press(screen.getByRole('button', { name: 'Confirm override' }));

    await waitFor(() => expect(spy).toHaveBeenCalledTimes(1));
    expect(JSON.parse((spy.mock.calls[0][1] as RequestInit).body as string)).toEqual({
      toStatus: 'Delivered',
      reason: 'Customer confirmed receipt',
    });
  });

  it('shows the server wording of a 409', async () => {
    await signInAs('Admin');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => problemResponse(409, { title: 'The order is no longer Paid' }));
    await render('Paid');

    await fireEvent.press(screen.getByRole('button', { name: 'Process' }));

    expect(await screen.findByText('The order is no longer Paid')).toBeOnTheScreen();
  });

  it('shows the refetched server status after success, never an optimistic one', async () => {
    await signInAs('Admin');
    let status: OrderStatus = 'Paid';
    jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      if (String(input).endsWith('/process')) {
        return noContent();
      }
      return jsonResponse(200, makeAdminOrderDetail({ order: makeOrderDetail({ id: 'o-9', status }) }));
    });
    function Probe() {
      const query = useAdminOrderQuery('o-9');
      return query.data ? <OrderActions detail={query.data} /> : null;
    }
    await renderRouterWithQuery({ index: Probe }, { initialUrl: '/' });
    await fireEvent.press(await screen.findByRole('button', { name: 'Process' }));

    // The server still reports Paid, so nothing flips locally: Process is still offered and Ship is not.
    await waitFor(() => expect(screen.getByRole('button', { name: 'Process' })).not.toBeDisabled());
    expect(screen.queryByRole('button', { name: 'Ship' })).toBeNull();

    status = 'Processing';
    await fireEvent.press(screen.getByRole('button', { name: 'Process' }));
    expect(await screen.findByRole('button', { name: 'Ship' })).toBeOnTheScreen();
  });

  it('closes the ship form once the server reports Shipped, offering Deliver', async () => {
    await signInAs('Admin');
    let status: OrderStatus = 'Processing';
    jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      if (String(input).endsWith('/ship')) {
        status = 'Shipped';
        return noContent();
      }
      return jsonResponse(200, makeAdminOrderDetail({ order: makeOrderDetail({ id: 'o-9', status }) }));
    });
    function Probe() {
      const query = useAdminOrderQuery('o-9');
      return query.data ? <OrderActions detail={query.data} /> : null;
    }
    await renderRouterWithQuery({ index: Probe }, { initialUrl: '/' });
    await fireEvent.press(await screen.findByRole('button', { name: 'Ship' }));
    await fireEvent.changeText(screen.getByLabelText('Carrier code'), 'example-post');
    await fireEvent.changeText(screen.getByLabelText('Tracking number'), 'T-123');
    await fireEvent.press(screen.getByRole('button', { name: 'Confirm ship' }));

    expect(await screen.findByRole('button', { name: 'Deliver' })).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Confirm ship' })).toBeNull();
    expect(screen.queryByLabelText('Carrier code')).toBeNull();
  });

  it('keeps the ship form and its values after a 409', async () => {
    await signInAs('Admin');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => problemResponse(409, { title: 'The order is no longer Processing' }));
    await render('Processing');
    await fireEvent.press(screen.getByRole('button', { name: 'Ship' }));
    await fireEvent.changeText(screen.getByLabelText('Carrier code'), 'example-post');
    await fireEvent.changeText(screen.getByLabelText('Tracking number'), 'T-123');
    await fireEvent.press(screen.getByRole('button', { name: 'Confirm ship' }));

    expect(await screen.findByText('The order is no longer Processing')).toBeOnTheScreen();
    expect(screen.getByLabelText('Carrier code')).toHaveDisplayValue('example-post');
    expect(screen.getByLabelText('Tracking number')).toHaveDisplayValue('T-123');
  });
});
