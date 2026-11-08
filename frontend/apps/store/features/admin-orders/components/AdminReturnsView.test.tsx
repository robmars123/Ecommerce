import { fireEvent, screen } from 'expo-router/testing-library';

import AdminReturnListScreen from '@/app/admin/returns';
import { resetSession } from '@/features/auth/session';
import { calledUrls, jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { makeReturnPage, makeReturnRow } from '@/tests/admin-order-fixtures';
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

const render = () => renderRouterWithQuery({ 'admin/returns': AdminReturnListScreen }, { initialUrl: '/admin/returns' });

describe('AdminReturnsView', () => {
  it('lists Requested returns first with the order, items and reason', async () => {
    await signInAs('Admin');
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeReturnPage([makeReturnRow()])));

    await render();

    expect(await screen.findByText('ORD-2026-000009 · Requested')).toBeOnTheScreen();
    expect(screen.getByText('Black tee, M × 1')).toBeOnTheScreen();
    expect(screen.getByText('Reason: Too small')).toBeOnTheScreen();
    expect(calledUrls(spy)[0]).toBe('http://api.test.invalid:5080/api/v1/admin/returns?status=Requested&page=1&pageSize=20');
  });

  it('shows an empty state', async () => {
    await signInAs('Admin');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeReturnPage([])));

    await render();

    expect(await screen.findByText('No return requests.')).toBeOnTheScreen();
  });

  it('changing the filter asks the server for that status, and "All" sends none', async () => {
    await signInAs('Admin');
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeReturnPage([makeReturnRow({ status: 'Approved' })])));
    await render();
    await screen.findByText('ORD-2026-000009 · Approved');

    await fireEvent.press(screen.getByRole('button', { name: 'Declined' }));
    await screen.findByText('ORD-2026-000009 · Approved');
    await fireEvent.press(screen.getByRole('button', { name: 'All' }));

    await screen.findByText('ORD-2026-000009 · Approved');
    const urls = calledUrls(spy);
    expect(urls.some((u) => u.includes('status=Declined'))).toBe(true);
    expect(urls.some((u) => u.endsWith('/api/v1/admin/returns?page=1&pageSize=20'))).toBe(true);
  });

  it('approve and decline need a note, send it, and refetch the list', async () => {
    await signInAs('Admin');
    let rows = [makeReturnRow()];
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async (_input, init) => {
      if ((init as RequestInit | undefined)?.method === 'POST') {
        rows = [];
        return new Response(null, { status: 204 });
      }
      return jsonResponse(200, makeReturnPage(rows));
    });
    await render();
    await screen.findByText('ORD-2026-000009 · Requested');
    expect(screen.getByRole('button', { name: 'Approve ORD-2026-000009' }).props.accessibilityState.disabled).toBe(true);

    await fireEvent.changeText(screen.getByLabelText('Decision note for ORD-2026-000009'), 'Within 30 days');
    await fireEvent.press(screen.getByRole('button', { name: 'Approve ORD-2026-000009' }));

    expect(await screen.findByText('No return requests.')).toBeOnTheScreen();
    const post = spy.mock.calls.find((c) => (c[1] as RequestInit | undefined)?.method === 'POST')!;
    expect(String(post[0])).toBe('http://api.test.invalid:5080/api/v1/admin/returns/r-1/approve');
    expect(JSON.parse((post[1] as RequestInit).body as string)).toEqual({ reason: 'Within 30 days' });
  });

  it('decline sends to the decline endpoint', async () => {
    await signInAs('Admin');
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async (_input, init) =>
      (init as RequestInit | undefined)?.method === 'POST' ? new Response(null, { status: 204 }) : jsonResponse(200, makeReturnPage([makeReturnRow()])),
    );
    await render();
    await screen.findByText('ORD-2026-000009 · Requested');

    await fireEvent.changeText(screen.getByLabelText('Decision note for ORD-2026-000009'), 'Past the window');
    await fireEvent.press(screen.getByRole('button', { name: 'Decline ORD-2026-000009' }));

    await screen.findByText('ORD-2026-000009 · Requested');
    expect(spy.mock.calls.some((c) => String(c[0]).endsWith('/api/v1/admin/returns/r-1/decline'))).toBe(true);
  });

  it('shows a plain message when a decision fails, e.g. someone decided first (409)', async () => {
    await signInAs('Admin');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async (_input, init) =>
      (init as RequestInit | undefined)?.method === 'POST' ? problemResponse(409, { title: 'Conflict' }) : jsonResponse(200, makeReturnPage([makeReturnRow()])),
    );
    await render();
    await screen.findByText('ORD-2026-000009 · Requested');

    await fireEvent.changeText(screen.getByLabelText('Decision note for ORD-2026-000009'), 'ok');
    await fireEvent.press(screen.getByRole('button', { name: 'Approve ORD-2026-000009' }));

    expect(await screen.findByText('That changed while you were editing. Reload and try again.')).toBeOnTheScreen();
  });

  it('only a Requested return can be decided; a decided one shows its note', async () => {
    await signInAs('Admin');
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () =>
      jsonResponse(200, makeReturnPage([makeReturnRow({ status: 'Declined', adminNote: 'Past the window' })])),
    );
    await render();

    await screen.findByText('Note: Past the window');
    expect(screen.queryByRole('button', { name: /^Approve ORD/ })).toBeNull();
  });

  it('pages through the list', async () => {
    await signInAs('Admin');
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeReturnPage([makeReturnRow()], { totalPages: 2, totalCount: 21 })));
    await render();
    await screen.findByText('Page 1 of 2');

    await fireEvent.press(screen.getByRole('button', { name: 'Next page' }));

    await screen.findByText('ORD-2026-000009 · Requested');
    expect(calledUrls(spy).some((u) => u.includes('page=2'))).toBe(true);
  });

  it('shows a plain error with Try again when loading fails', async () => {
    await signInAs('Admin');
    jest
      .spyOn(globalThis, 'fetch')
      .mockImplementationOnce(async () => problemResponse(500, { title: 'boom' }))
      .mockImplementation(async () => jsonResponse(200, makeReturnPage([makeReturnRow()])));
    await render();

    await fireEvent.press(await screen.findByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('ORD-2026-000009 · Requested')).toBeOnTheScreen();
  });
});
