import { fireEvent, screen } from 'expo-router/testing-library';

import AdminOrderDetailScreen from '@/app/admin/orders/[id]';
import { resetSession } from '@/features/auth/session';
import { makeAdminOrderDetail, usd } from '@/tests/admin-order-fixtures';
import { jsonResponse, problemResponse } from '@/tests/fetch-mock';
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

const render = () => renderRouterWithQuery({ 'admin/orders/[id]': AdminOrderDetailScreen }, { initialUrl: '/admin/orders/o-9' });

describe('AdminOrderDetailView', () => {
  it('shows the order, the server-computed refund totals and every refund with its status', async () => {
    await signInAs('Admin');
    const detail = makeAdminOrderDetail({
      refunds: [
        { refundId: 'rf-1', amount: usd('10.00'), status: 'Succeeded', createdAt: '2026-03-07T10:00:00Z' },
        { refundId: 'rf-2', amount: usd('5.00'), status: 'Pending', createdAt: '2026-03-08T10:00:00Z' },
      ],
      refundedTotal: usd('10.00'),
      pendingTotal: usd('5.00'),
      remaining: usd('30.00'),
    });
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, detail));

    await render();

    expect(await screen.findByText('ORD-2026-000001')).toBeOnTheScreen();
    expect(screen.getByText('Refunded $10.00')).toBeOnTheScreen();
    expect(screen.getByText('Pending $5.00')).toBeOnTheScreen();
    expect(screen.getByText('Remaining $30.00')).toBeOnTheScreen();
    expect(screen.getByText('$10.00 · Refunded')).toBeOnTheScreen();
    expect(screen.getByText('$5.00 · Being processed')).toBeOnTheScreen();
    expect(screen.getByText('Remaining to refund: $30.00')).toBeOnTheScreen();
  });

  it('lists the return requests of the order', async () => {
    await signInAs('Admin');
    const detail = makeAdminOrderDetail({
      returnRequests: [{ id: 'r-1', orderId: 'o-9', orderNumber: 'N', status: 'Approved', reason: 'Too small', adminNote: 'ok', items: [], createdAt: '2026-03-10T10:00:00Z', decidedAt: null }],
    });
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, detail));

    await render();

    expect(await screen.findByText('Approved: Too small')).toBeOnTheScreen();
  });

  it('asks the server again after a refund so the new Pending and Remaining come from it (no optimistic status)', async () => {
    await signInAs('Admin');
    let detail = makeAdminOrderDetail();
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      if ((init as RequestInit | undefined)?.method === 'POST') {
        detail = makeAdminOrderDetail({
          refunds: [{ refundId: 'rf-1', amount: usd('12.50'), status: 'Pending', createdAt: '2026-03-08T10:00:00Z' }],
          pendingTotal: usd('12.50'),
          remaining: usd('32.50'),
        });
        return jsonResponse(202, { refundId: 'rf-1', amount: usd('12.50'), status: 'Pending', refundedTotal: usd('0.00'), remaining: usd('32.50') });
      }
      return jsonResponse(200, detail);
    });
    await render();
    await screen.findByText('Pending $0.00');

    await fireEvent.changeText(screen.getByLabelText('Refund amount (USD)'), '12.50');
    await fireEvent.changeText(screen.getByLabelText('Refund reason'), 'Damaged');
    await fireEvent.press(screen.getByRole('button', { name: 'Refund' }));

    expect(await screen.findByText('Pending $12.50')).toBeOnTheScreen();
    expect(screen.getByText('Remaining $32.50')).toBeOnTheScreen();
    expect(spy.mock.calls.filter((c) => (c[1] as RequestInit | undefined)?.method === 'POST')).toHaveLength(1);
  });

  it('says it could not find an unknown order, and offers a retry for a server error', async () => {
    await signInAs('Admin');
    jest.spyOn(globalThis, 'fetch').mockImplementationOnce(async () => problemResponse(404, { title: 'Order not found' }));
    await render();
    expect(await screen.findByText('We could not find that order.')).toBeOnTheScreen();
  });

  it('shows a plain error with Try again when the server fails', async () => {
    await signInAs('Admin');
    jest
      .spyOn(globalThis, 'fetch')
      .mockImplementationOnce(async () => problemResponse(500, { title: 'boom' }))
      .mockImplementation(async () => jsonResponse(200, makeAdminOrderDetail()));
    await render();

    await fireEvent.press(await screen.findByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('ORD-2026-000001')).toBeOnTheScreen();
  });

  it('shows the fulfillment actions to an Admin', async () => {
    await signInAs('Admin');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeAdminOrderDetail()));

    await render();

    expect(await screen.findByRole('button', { name: 'Process' })).toBeOnTheScreen();
  });
});
