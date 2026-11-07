import { fireEvent, screen, waitFor } from 'expo-router/testing-library';

import ProductListScreen from '@/app/products/index';
import { calledUrls, jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { categories, makeItem, makePage } from '@/tests/fixtures';
import { renderRouterWithQuery } from '@/tests/render-with-query';

const BASE = 'http://api.test.invalid:5080';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = BASE;
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

type ProductsHandler = (url: URL) => Response;

function mockApi(products: ProductsHandler) {
  return jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const url = new URL(String(input));
    if (url.pathname === '/api/v1/categories') return jsonResponse(200, { items: categories });
    if (url.pathname === '/api/v1/products') return products(url);
    return new Response('not mocked', { status: 599 });
  });
}

function productUrls(spy: jest.SpyInstance): URL[] {
  return calledUrls(spy)
    .map((u) => new URL(u))
    .filter((u) => u.pathname === '/api/v1/products');
}

const render = (initialUrl: string) =>
  renderRouterWithQuery({ 'products/index': ProductListScreen }, { initialUrl });

describe('ProductListView', () => {
  it('shows a loading indicator, then the products with price and category', async () => {
    mockApi(() => jsonResponse(200, makePage([makeItem(), makeItem({ id: 'p-2', slug: 'red-shirt', name: 'Red Shirt' })])));

    await render('/products');

    expect(screen.getByLabelText('Loading')).toBeOnTheScreen();
    expect(await screen.findByRole('link', { name: 'Blue Shirt' })).toBeOnTheScreen();
    expect(screen.getByRole('link', { name: 'Red Shirt' })).toBeOnTheScreen();
    expect(screen.getByText('2 products')).toBeOnTheScreen();
    expect(screen.queryByLabelText('Loading')).toBeNull();
  });

  it('reads search, category, sort and page from the URL and sends all of them to the server', async () => {
    const spy = mockApi(() => jsonResponse(200, makePage([makeItem()], { page: 2, totalPages: 3, totalCount: 41 })));

    await render('/products?search=shirt&category=tops&sort=price-asc&page=2');
    await screen.findByRole('link', { name: 'Blue Shirt' });

    const url = productUrls(spy)[0];
    expect(url.searchParams.get('search')).toBe('shirt');
    expect(url.searchParams.get('category')).toBe('tops');
    expect(url.searchParams.get('sort')).toBe('price-asc');
    expect(url.searchParams.get('page')).toBe('2');
    expect(url.searchParams.get('pageSize')).toBe('20');
  });

  it('never filters on the client: the list shows exactly what the server returned', async () => {
    mockApi(() => jsonResponse(200, makePage([makeItem({ name: 'Unrelated Name' })])));

    await render('/products?search=zzz');

    expect(await screen.findByRole('link', { name: 'Unrelated Name' })).toBeOnTheScreen();
  });

  it('typing a search updates the URL after the debounce and resets to page 1', async () => {
    const spy = mockApi(() => jsonResponse(200, makePage([makeItem()], { page: 3, totalPages: 5, totalCount: 100 })));
    await render('/products?page=3');
    await screen.findByRole('link', { name: 'Blue Shirt' });

    await fireEvent.changeText(screen.getByLabelText('Search products'), 'jeans');
    expect(screen).toHaveSearchParams({ page: '3' });

    await waitFor(() => expect(screen).toHaveSearchParams({ search: 'jeans' }), { timeout: 3000 });
    const last = productUrls(spy).at(-1)!;
    expect(last.searchParams.get('search')).toBe('jeans');
    expect(last.searchParams.get('page')).toBe('1');
  });

  it('choosing a category and a sort updates the URL and the request', async () => {
    const spy = mockApi(() => jsonResponse(200, makePage([makeItem()])));
    await render('/products');
    await screen.findByRole('link', { name: 'Blue Shirt' });

    await fireEvent.press(await screen.findByRole('button', { name: 'Tops' }));
    await waitFor(() => expect(screen).toHaveSearchParams({ category: 'tops' }));

    await fireEvent.press(screen.getByRole('button', { name: 'Price: low to high' }));
    await waitFor(() => expect(screen).toHaveSearchParams({ category: 'tops', sort: 'price-asc' }));

    await waitFor(() => {
      const last = productUrls(spy).at(-1)!;
      expect(last.searchParams.get('category')).toBe('tops');
      expect(last.searchParams.get('sort')).toBe('price-asc');
    });
  });

  it('Next and Previous change the page in the URL and the request', async () => {
    const spy = mockApi((url) => {
      const page = Number(url.searchParams.get('page') ?? '1');
      return jsonResponse(200, makePage([makeItem({ id: `p-${page}`, slug: `item-${page}`, name: `Item ${page}` })], { page, totalPages: 3, totalCount: 3 }));
    });
    await render('/products');
    await screen.findByRole('link', { name: 'Item 1' });
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled();

    await fireEvent.press(screen.getByRole('button', { name: 'Next' }));
    expect(await screen.findByRole('link', { name: 'Item 2' })).toBeOnTheScreen();
    expect(screen).toHaveSearchParams({ page: '2' });
    expect(productUrls(spy).at(-1)!.searchParams.get('page')).toBe('2');

    await fireEvent.press(screen.getByRole('button', { name: 'Previous' }));
    expect(await screen.findByRole('link', { name: 'Item 1' })).toBeOnTheScreen();
    expect(screen).toHaveSearchParams({});
  });

  it('shows the empty state and Clear filters resets every filter', async () => {
    const spy = mockApi((url) =>
      url.searchParams.get('search') ? jsonResponse(200, makePage([])) : jsonResponse(200, makePage([makeItem()])),
    );
    await render('/products?search=zzz&category=tops&sort=price-desc&page=2');

    expect(await screen.findByText('No products match your search.')).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: 'Clear filters' }));

    expect(await screen.findByRole('link', { name: 'Blue Shirt' })).toBeOnTheScreen();
    expect(screen).toHaveSearchParams({});
    expect(productUrls(spy).at(-1)!.searchParams.has('search')).toBe(false);
  });

  it('a page past the end offers Go to first page', async () => {
    mockApi((url) =>
      url.searchParams.get('page') === '9'
        ? jsonResponse(200, makePage([], { page: 9, totalCount: 25, totalPages: 2 }))
        : jsonResponse(200, makePage([makeItem()])),
    );
    await render('/products?page=9');

    expect(await screen.findByText('There are no products on that page.')).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: 'Go to first page' }));

    expect(await screen.findByRole('link', { name: 'Blue Shirt' })).toBeOnTheScreen();
  });

  it('shows a plain-language error with Retry, and Retry fetches again', async () => {
    let fail = true;
    const spy = mockApi(() => (fail ? problemResponse(500, { title: 'boom' }) : jsonResponse(200, makePage([makeItem()]))));
    await render('/products');

    expect(await screen.findByText('Something went wrong on our side. Please try again.')).toBeOnTheScreen();
    expect(screen.getByRole('alert')).toBeOnTheScreen();
    expect(productUrls(spy)).toHaveLength(1);

    fail = false;
    await fireEvent.press(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByRole('link', { name: 'Blue Shirt' })).toBeOnTheScreen();
    expect(productUrls(spy)).toHaveLength(2);
  });

  it('keeps the Products header', async () => {
    mockApi(() => jsonResponse(200, makePage([])));
    await render('/products');

    expect(screen.getAllByRole('header', { name: 'Products' }).length).toBeGreaterThanOrEqual(1);
  });
});

describe('ProductListView count line', () => {
  it('shows the visible range and the unchanged total, with one Products header', async () => {
    mockApi(() => jsonResponse(200, makePage([makeItem(), makeItem({ id: 'p-2', slug: 'red-shirt', name: 'Red Shirt' })])));
    await render('/products');

    expect(await screen.findByText('Showing 1–2 of')).toBeOnTheScreen();
    expect(screen.getByText('2 products')).toBeOnTheScreen();
    expect(screen.getAllByRole('header', { name: 'Products' })).toHaveLength(1);
  });
});
