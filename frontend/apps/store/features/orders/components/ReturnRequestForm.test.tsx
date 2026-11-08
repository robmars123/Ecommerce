import { fireEvent, screen } from 'expo-router/testing-library';

import { jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { makeOrderDetail } from '@/tests/order-fixtures';
import { renderRouterWithQuery } from '@/tests/render-with-query';

import { ReturnRequestForm } from './ReturnRequestForm';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const delivered = makeOrderDetail({ status: 'Delivered' });
const QTY = 'Return how many of Black tee, M (up to 2)';

const render = (order = delivered) => renderRouterWithQuery({ index: () => <ReturnRequestForm order={order} /> }, { initialUrl: '/' });

async function fill(quantity = '1', reason = 'Too small') {
  await fireEvent.changeText(screen.getByLabelText(QTY), quantity);
  await fireEvent.changeText(screen.getByLabelText('Why are you returning it'), reason);
}

// F8: no optimistic submit; a server error shows plain text with a retry; the form shows on Delivered only.
describe('ReturnRequestForm', () => {
  it.each(['PendingPayment', 'Paid', 'Processing', 'Shipped', 'Cancelled', 'Refunded', 'Returned'] as const)(
    'does not show for a %s order',
    async (status) => {
      await render(makeOrderDetail({ status }));

      expect(screen.queryByText('Return items')).toBeNull();
      expect(screen.queryByRole('button', { name: 'Request a return' })).toBeNull();
    },
  );

  it('shows for a Delivered order, one field per line', async () => {
    await render();

    expect(screen.getByText('Return items')).toBeOnTheScreen();
    expect(screen.getByLabelText(QTY)).toBeOnTheScreen();
  });

  it('sends the chosen items and the trimmed reason, and says "requested" only after the server created it', async () => {
    let answer: (response: Response) => void = () => undefined;
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(() => new Promise<Response>((resolve) => (answer = resolve)));
    await render();
    await fill('1', '  Too small ');

    await fireEvent.press(screen.getByRole('button', { name: 'Request a return' }));

    // No optimistic state: nothing says "requested" while the request is in flight.
    expect(screen.queryByText(/Return requested/)).toBeNull();
    const [url, init] = spy.mock.calls[0] as [string, RequestInit];
    expect(url).toBe('http://api.test.invalid:5080/api/v1/orders/o-1/return-request');
    expect(JSON.parse(init.body as string)).toEqual({ items: [{ variantId: 'v-1', quantity: 1 }], reason: 'Too small' });
    expect((init.headers as Record<string, string>)['Idempotency-Key']).toBeUndefined();

    answer(jsonResponse(201, { returnId: 'r-1' }));
    expect(await screen.findByText(/Return requested/)).toBeOnTheScreen();
  });

  it('a double press sends one request', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(() => new Promise<Response>(() => undefined));
    await render();
    await fill();

    await fireEvent.press(screen.getByRole('button', { name: 'Request a return' }));
    await fireEvent.press(screen.getByRole('button', { name: 'Request a return' }));

    expect(spy).toHaveBeenCalledTimes(1);
  });

  it('asks for a quantity and a reason before sending anything', async () => {
    const spy = jest.spyOn(globalThis, 'fetch');
    await render();

    await fireEvent.press(screen.getByRole('button', { name: 'Request a return' }));
    expect(await screen.findByText('Enter how many of at least one item you want to return.')).toBeOnTheScreen();
    await fireEvent.changeText(screen.getByLabelText(QTY), '1');
    await fireEvent.press(screen.getByRole('button', { name: 'Request a return' }));
    expect(await screen.findByText('Tell us why, in 1 to 500 characters.')).toBeOnTheScreen();
    expect(spy).not.toHaveBeenCalled();
  });

  it('shows a plain-language message for a server error and a retry that sends again', async () => {
    const spy = jest
      .spyOn(globalThis, 'fetch')
      .mockImplementationOnce(async () => problemResponse(500, { title: 'boom' }))
      .mockImplementationOnce(async () => jsonResponse(201, { returnId: 'r-1' }));
    await render();
    await fill();

    await fireEvent.press(screen.getByRole('button', { name: 'Request a return' }));
    expect(await screen.findByText('Something went wrong on our side. Please try again.')).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText(/Return requested/)).toBeOnTheScreen();
    expect(spy).toHaveBeenCalledTimes(2);
  });

  it('explains a second open request in plain language', async () => {
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => problemResponse(409, { title: 'x', type: 'return-already-open' }));
    await render();
    await fill();

    await fireEvent.press(screen.getByRole('button', { name: 'Request a return' }));

    expect(await screen.findByText('You already have an open return request for this order.')).toBeOnTheScreen();
  });
});
