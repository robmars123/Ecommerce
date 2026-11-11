import { act, fireEvent, screen, within } from 'expo-router/testing-library';
import { Text } from 'react-native';

import type { UserRole } from '@/features/auth/types';
import { makeSearchResult } from '@/tests/admin-search-fixtures';
import { calledUrls, jsonResponse, mockFetchRoutes, problemResponse } from '@/tests/fetch-mock';
import { renderRouterWithQuery } from '@/tests/render-with-query';
import { settleFetch } from '@/tests/settle';

import { AdminSearch } from './AdminSearch';

async function renderSearch(role: UserRole = 'Admin') {
  const stub = (name: string) => () => <Text>{`${name} page`}</Text>;
  return await renderRouterWithQuery(
    {
      index: () => <AdminSearch role={role} />,
      'admin/orders/[id]': stub('Order'),
      'admin/products/[id]': stub('Product'),
    },
    { initialUrl: '/' },
  );
}

async function type(text: string) {
  await fireEvent.changeText(screen.getByLabelText('Search admin'), text);
}

async function advance(ms: number) {
  await act(() => jest.advanceTimersByTimeAsync(ms));
  await settleFetch();
}

function panel() {
  return within(screen.getByLabelText('Search results'));
}

function searchSpy(body: unknown = makeSearchResult()) {
  return mockFetchRoutes({ '/api/v1/admin/search': () => jsonResponse(200, body) });
}

function qs(spy: jest.SpyInstance): string[] {
  return calledUrls(spy).map((u) => new URL(u).searchParams.get('q') ?? '');
}

beforeEach(() => {
  jest.useFakeTimers();
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.useRealTimers();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

describe('AdminSearch', () => {
  it('C1 one character shows the hint and sends nothing; empty shows no panel', async () => {
    const spy = searchSpy();
    await renderSearch();
    await type('a');
    await advance(1000);
    expect(panel().getByText('Type at least 2 characters.')).toBeTruthy();
    expect(spy).not.toHaveBeenCalled();
    await type('');
    expect(screen.queryByLabelText('Search results')).toBeNull();
  });

  it('C2 debounces 300 ms and sends the trimmed latest term only', async () => {
    const spy = searchSpy();
    await renderSearch();
    await type('ab');
    await advance(299);
    expect(spy).toHaveBeenCalledTimes(0);
    await advance(1);
    expect(qs(spy)).toEqual(['ab']);
    await type('abc');
    await advance(100);
    await type('abcd');
    await advance(300);
    expect(qs(spy)).toEqual(['ab', 'abcd']);
    await type('  ef ');
    await advance(300);
    expect(qs(spy)[2]).toBe('ef');
  });

  it('C3 shows Loading while the request is pending', async () => {
    jest.spyOn(globalThis, 'fetch').mockImplementation(() => new Promise<Response>(() => undefined));
    await renderSearch();
    await type('ab');
    await advance(300);
    expect(panel().getByLabelText('Loading')).toBeTruthy();
    expect(panel().queryByRole('header')).toBeNull();
  });

  it('C4 Admin sees all groups; pressing an order navigates and closes the panel', async () => {
    searchSpy();
    await renderSearch();
    await type('ab');
    await advance(300);
    const p = panel();
    expect(p.getByRole('header', { name: 'Orders' })).toBeTruthy();
    expect(p.getByRole('header', { name: 'Products' })).toBeTruthy();
    expect(p.getByRole('header', { name: 'SKUs' })).toBeTruthy();
    expect(p.getByRole('link', { name: '1001 · Paid' })).toBeTruthy();
    expect(p.getByRole('link', { name: 'Blue Mug' })).toBeTruthy();
    expect(p.getByRole('link', { name: 'MUG-BLU-L · Blue Mug' })).toBeTruthy();
    await fireEvent.press(p.getByRole('link', { name: '1001 · Paid' }));
    expect(screen).toHavePathname('/admin/orders/o1');
    expect(screen.queryByLabelText('Search results')).toBeNull();
  });

  it('C5 a SKU links to its product page', async () => {
    searchSpy();
    await renderSearch();
    await type('ab');
    await advance(300);
    await fireEvent.press(panel().getByRole('link', { name: 'MUG-BLU-L · Blue Mug' }));
    expect(screen).toHavePathname('/admin/products/p1');
  });

  it('C6 shows the empty copy and no header', async () => {
    searchSpy(makeSearchResult({ orders: [], products: [], skus: [] }));
    await renderSearch();
    await type('ab');
    await advance(300);
    expect(panel().getByText('No matches for “ab”.')).toBeTruthy();
    expect(panel().queryByRole('header')).toBeNull();
  });

  it('C7 shows a fixed error, retries on demand and then shows the groups', async () => {
    const spy = mockFetchRoutes({ '/api/v1/admin/search': () => problemResponse(404, { title: 'Not found' }) });
    await renderSearch();
    await type('ab');
    await advance(300);
    expect(panel().getByRole('alert')).toHaveTextContent("Search isn't available right now.");
    expect(spy).toHaveBeenCalledTimes(1);
    await fireEvent.press(panel().getByRole('button', { name: 'Try again' }));
    await advance(0);
    expect(spy).toHaveBeenCalledTimes(2);
    searchSpy();
    await fireEvent.press(panel().getByRole('button', { name: 'Try again' }));
    await advance(0);
    expect(panel().getByRole('link', { name: '1001 · Paid' })).toBeTruthy();
  });

  it('C8 Support sees Orders only, and the no-match copy when only catalog groups arrive', async () => {
    searchSpy();
    await renderSearch('Support');
    await type('ab');
    await advance(300);
    expect(panel().getByRole('header', { name: 'Orders' })).toBeTruthy();
    expect(panel().getByRole('link', { name: '1001 · Paid' })).toBeTruthy();
    expect(panel().queryByRole('header', { name: 'Products' })).toBeNull();
    expect(panel().queryByRole('header', { name: 'SKUs' })).toBeNull();
    expect(panel().queryByRole('link', { name: /Blue Mug/ })).toBeNull();

    searchSpy(makeSearchResult({ orders: [] }));
    await type('abc');
    await advance(300);
    expect(panel().getByText('No matches for “abc”.')).toBeTruthy();
  });

  it('C9 Escape hides the panel and keeps the text; typing reopens it', async () => {
    const spy = searchSpy();
    await renderSearch();
    await type('ab');
    await advance(300);
    expect(spy).toHaveBeenCalledTimes(1);
    await fireEvent(screen.getByLabelText('Search admin'), 'keyPress', { nativeEvent: { key: 'Escape' } });
    expect(screen.queryByLabelText('Search results')).toBeNull();
    expect(screen.getByLabelText('Search admin').props.value).toBe('ab');
    expect(spy).toHaveBeenCalledTimes(1);
    await type('abc');
    expect(screen.getByLabelText('Search results')).toBeTruthy();
  });

  it('C10 Clear empties the input and closes the panel; it is absent when empty', async () => {
    const spy = searchSpy();
    await renderSearch();
    expect(screen.queryByRole('button', { name: 'Clear search' })).toBeNull();
    await type('ab');
    await advance(300);
    await fireEvent.press(screen.getByRole('button', { name: 'Clear search' }));
    expect(screen.getByLabelText('Search admin').props.value).toBe('');
    expect(screen.queryByLabelText('Search results')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Clear search' })).toBeNull();
    await advance(1000);
    expect(spy).toHaveBeenCalledTimes(1);
  });

  it('C11 a late answer for an older term never shows; maxLength is 100', async () => {
    let releaseAb: (r: Response) => void = () => undefined;
    jest.spyOn(globalThis, 'fetch').mockImplementation((input) => {
      const q = new URL(String(input)).searchParams.get('q');
      if (q === 'ab') return new Promise<Response>((resolve) => (releaseAb = resolve));
      return Promise.resolve(
        jsonResponse(200, makeSearchResult({ orders: [{ id: 'o2', orderNumber: '1002', status: 'Paid' }], products: [], skus: [] })),
      );
    });
    await renderSearch();
    expect(screen.getByLabelText('Search admin').props.maxLength).toBe(100);
    await type('ab');
    await advance(300);
    await type('abc');
    await advance(300);
    await act(async () => releaseAb(jsonResponse(200, makeSearchResult())));
    await advance(0);
    expect(panel().getByRole('link', { name: '1002 · Paid' })).toBeTruthy();
    expect(panel().queryByRole('link', { name: '1001 · Paid' })).toBeNull();
  });
});
