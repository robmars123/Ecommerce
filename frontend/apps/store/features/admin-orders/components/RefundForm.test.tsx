import { fireEvent, screen } from 'expo-router/testing-library';

import { jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { makeAdminOrderDetail, usd } from '@/tests/admin-order-fixtures';
import { renderRouterWithQuery } from '@/tests/render-with-query';

import { RefundForm } from './RefundForm';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const accepted = { refundId: 'rf-1', amount: usd('12.50'), status: 'Pending', refundedTotal: usd('0.00'), remaining: usd('32.50') };
const render = () => renderRouterWithQuery({ index: () => <RefundForm detail={makeAdminOrderDetail()} /> }, { initialUrl: '/' });

async function fill(amount = '12.50', reason = 'Damaged in transit') {
  await fireEvent.changeText(screen.getByLabelText('Refund amount (USD)'), amount);
  await fireEvent.changeText(screen.getByLabelText('Refund reason'), reason);
}

const press = () => fireEvent.press(screen.getByRole('button', { name: 'Refund' }));

function keyOf(spy: jest.SpyInstance, call: number): string {
  return ((spy.mock.calls[call][1] as RequestInit).headers as Record<string, string>)['Idempotency-Key'];
}

// F13: one Idempotency-Key per refund attempt, reused on retry; the amount is a decimal string; no optimistic status.
describe('RefundForm', () => {
  it('sends the amount as a decimal string with the order currency and an Idempotency-Key', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(202, accepted));
    await render();
    await fill(' 12.50 ');

    await press();

    expect(await screen.findByText(/Refund of \$12\.50 requested/)).toBeOnTheScreen();
    const [url, init] = spy.mock.calls[0] as [string, RequestInit];
    expect(url).toBe('http://api.test.invalid:5080/api/v1/admin/orders/o-9/refund');
    expect(JSON.parse(init.body as string)).toEqual({ amount: { amount: '12.50', currency: 'USD' }, reason: 'Damaged in transit' });
    expect(typeof JSON.parse(init.body as string).amount.amount).toBe('string');
    expect(keyOf(spy, 0)).toMatch(/^[0-9a-f-]{36}$/);
  });

  it('reuses the key when the same attempt is retried after a network failure', async () => {
    const spy = jest
      .spyOn(globalThis, 'fetch')
      .mockImplementationOnce(async () => {
        throw new TypeError('network down');
      })
      .mockImplementationOnce(async () => jsonResponse(202, accepted));
    await render();
    await fill();

    await press();
    expect(await screen.findByText("Can't reach the store. Check your connection and try again.")).toBeOnTheScreen();
    await press();

    expect(await screen.findByText(/Refund of \$12\.50 requested/)).toBeOnTheScreen();
    expect(spy).toHaveBeenCalledTimes(2);
    expect(keyOf(spy, 1)).toBe(keyOf(spy, 0));
  });

  it('uses a new key for the next attempt after a definite answer (a 409 the admin corrected)', async () => {
    const spy = jest
      .spyOn(globalThis, 'fetch')
      .mockImplementationOnce(async () => problemResponse(409, { title: 'Refund not allowed', type: 'refund-ceiling-exceeded' }))
      .mockImplementationOnce(async () => jsonResponse(202, accepted));
    await render();
    await fill('99.00');

    await press();
    await screen.findByText('That is more than is left to refund. Lower the amount.');
    await fill('12.50');
    await press();

    await screen.findByText(/Refund of \$12\.50 requested/);
    expect(keyOf(spy, 1)).not.toBe(keyOf(spy, 0));
  });

  it('shows the server reason for a 409, in plain language', async () => {
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => problemResponse(409, { title: 'x', type: 'refund-state-not-allowed' }));
    await render();
    await fill();

    await press();

    expect(await screen.findByText('This order cannot be refunded that way in its current state.')).toBeOnTheScreen();
  });

  it('shows no refund status until the server answers', async () => {
    jest.spyOn(globalThis, 'fetch').mockImplementation(() => new Promise<Response>(() => undefined));
    await render();
    await fill();

    await press();

    expect(screen.queryByText(/requested/)).toBeNull();
    expect(screen.queryByText(/Pending/)).toBeNull();
  });

  it('a double press sends one request', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(() => new Promise<Response>(() => undefined));
    await render();
    await fill();

    await press();
    await press();

    expect(spy).toHaveBeenCalledTimes(1);
  });

  it.each(['', '0', '0.00', '-5', 'abc', '1e2', '12.345', '1,000.00'])('does not send an amount of "%s"', async (amount) => {
    const spy = jest.spyOn(globalThis, 'fetch');
    await render();
    await fill(amount);

    await press();

    expect(await screen.findByText('Enter an amount greater than zero, such as 12.50.')).toBeOnTheScreen();
    expect(spy).not.toHaveBeenCalled();
  });

  it('needs a reason', async () => {
    const spy = jest.spyOn(globalThis, 'fetch');
    await render();
    await fill('5.00', '  ');

    await press();

    expect(await screen.findByText('Enter a reason of 1 to 500 characters.')).toBeOnTheScreen();
    expect(spy).not.toHaveBeenCalled();
  });

  it('shows the remaining amount the server computed', async () => {
    await render();

    expect(screen.getByText('Remaining to refund: $45.00')).toBeOnTheScreen();
  });
});
