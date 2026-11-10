import { fireEvent, screen, waitFor } from 'expo-router/testing-library';
import { Text } from 'react-native';

import AdminCustomerListScreen from '@/app/admin/customers/index';
import { resetSession } from '@/features/auth/session';
import { makeCustomerItem, makeCustomerPage } from '@/tests/admin-customer-fixtures';
import { calledUrls, jsonResponse, mockFetchRoutes, problemResponse } from '@/tests/fetch-mock';
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

const render = (url = '/admin/customers') =>
  renderRouterWithQuery(
    { 'admin/customers/index': AdminCustomerListScreen, 'admin/customers/[id]': () => <Text>Customer detail screen</Text> },
    { initialUrl: url },
  );

const params = (spy: jest.SpyInstance, call: number) => Object.fromEntries(new URL(calledUrls(spy)[call]).searchParams);
const lastParams = (spy: jest.SpyInstance) => params(spy, spy.mock.calls.length - 1);
const rows = [makeCustomerItem(), makeCustomerItem({ id: 'c-2', email: 'bob@example.test', displayName: 'Bob Ray' })];
const serve = (page: () => unknown) => mockFetchRoutes({ '/api/v1/admin/customers': () => jsonResponse(200, page()) });

describe('AdminCustomerListView', () => {
  it('L1 renders each customer with name, email and join date, linking to the detail', async () => {
    await signInAs('Admin');
    serve(() => makeCustomerPage(rows));

    await render();

    expect(await screen.findByText('Ann Lee · ann@example.test · Joined Jan 5, 2026')).toBeOnTheScreen();
    expect(screen.getByText(/Bob Ray · bob@example\.test · Joined /)).toBeOnTheScreen();
    await fireEvent.press(screen.getByText(/Bob Ray/));
    expect(await screen.findByText('Customer detail screen')).toBeOnTheScreen();
  });

  it('L2 searches after the debounce, back on page 1, and sends no search for blank text', async () => {
    await signInAs('Admin');
    const spy = serve(() => makeCustomerPage(rows, { page: 3, totalPages: 5, totalCount: 100 }));
    await render('/admin/customers?page=3');
    await screen.findByText(/Ann Lee/);

    await fireEvent.changeText(screen.getByLabelText('Search products'), 'ann');
    await waitFor(() => expect(lastParams(spy)).toMatchObject({ search: 'ann', page: '1' }), { timeout: 3000 });

    await fireEvent.changeText(screen.getByLabelText('Search products'), '   ');
    await waitFor(() => expect(lastParams(spy).search).toBeUndefined(), { timeout: 3000 });
  });

  it('L2 sends at most 100 characters of a long search', async () => {
    await signInAs('Admin');
    const spy = serve(() => makeCustomerPage(rows));

    await render(`/admin/customers?search=${'a'.repeat(120)}`);

    await screen.findByText(/Ann Lee/);
    expect(params(spy, 0).search).toHaveLength(100);
  });

  it('L3 pages with Next, and hides the pager for a single page', async () => {
    await signInAs('Admin');
    const spy = serve(() => makeCustomerPage(rows, { totalPages: 3, totalCount: 50 }));
    await render();
    await screen.findByText(/Ann Lee/);

    await fireEvent.press(screen.getByRole('button', { name: 'Next' }));

    await waitFor(() => expect(lastParams(spy).page).toBe('2'));
  });

  it('L3 shows no pager when there is one page', async () => {
    await signInAs('Admin');
    serve(() => makeCustomerPage(rows));

    await render();

    await screen.findByText(/Ann Lee/);
    expect(screen.queryByRole('button', { name: 'Next' })).toBeNull();
  });

  it('L3 keeps the pager on a page past the end', async () => {
    await signInAs('Admin');
    serve(() => makeCustomerPage([], { page: 5, totalPages: 2, totalCount: 30 }));

    await render('/admin/customers?page=5');

    expect(await screen.findByText('No customers match.')).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Previous' })).toBeOnTheScreen();
  });

  it('L4 shows the empty state', async () => {
    await signInAs('Admin');
    serve(() => makeCustomerPage([]));

    await render();

    expect(await screen.findByText('No customers match.')).toBeOnTheScreen();
  });

  it('L4 shows an error whose Try again refetches', async () => {
    await signInAs('Admin');
    let fail = true;
    mockFetchRoutes({
      '/api/v1/admin/customers': () => (fail ? problemResponse(500, { title: 'boom' }) : jsonResponse(200, makeCustomerPage(rows))),
    });
    await render();
    const retry = await screen.findByRole('button', { name: 'Try again' });

    fail = false;
    await fireEvent.press(retry);

    expect(await screen.findByText(/Ann Lee/)).toBeOnTheScreen();
  });

  it('L4 shows the error, not page 1 rows, when page 2 fails', async () => {
    await signInAs('Admin');
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) =>
      new URL(String(input)).searchParams.get('page') === '2'
        ? problemResponse(500, { title: 'boom' })
        : jsonResponse(200, makeCustomerPage(rows, { totalPages: 3, totalCount: 50 })),
    );
    await render();
    await screen.findByText(/Ann Lee/);

    await fireEvent.press(screen.getByRole('button', { name: 'Next' }));

    expect(await screen.findByRole('button', { name: 'Try again' })).toBeOnTheScreen();
    expect(screen.queryByText(/Ann Lee/)).toBeNull();
    expect(spy).toHaveBeenCalled();
  });

  it('L5 reads search and page from the URL, and falls back to page 1 for junk', async () => {
    await signInAs('Admin');
    const spy = serve(() => makeCustomerPage(rows));

    await render('/admin/customers?search=bob&page=2');
    await screen.findByText(/Ann Lee/);
    expect(params(spy, 0)).toMatchObject({ search: 'bob', page: '2' });
  });

  it('L5 treats a non-numeric page as 1', async () => {
    await signInAs('Admin');
    const spy = serve(() => makeCustomerPage(rows));

    await render('/admin/customers?page=abc');

    await screen.findByText(/Ann Lee/);
    expect(params(spy, 0).page).toBe('1');
  });
});
