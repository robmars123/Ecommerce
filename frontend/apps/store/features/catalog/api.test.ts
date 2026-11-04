import { jsonResponse } from '@/tests/fetch-mock';

import { fetchCategories, fetchProduct, fetchProducts } from './api';

const BASE = 'http://api.test.invalid:5080';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = BASE;
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

function spyFetch() {
  return jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, {}));
}

describe('catalog api', () => {
  it('fetchProducts sends search, category, sort, page and pageSize to the server', async () => {
    const spy = spyFetch();

    await fetchProducts({ search: 'blue shirt', category: 'tops', sort: 'price-asc', page: 2, pageSize: 20 });

    expect(String(spy.mock.calls[0][0])).toBe(
      `${BASE}/api/v1/products?search=blue%20shirt&category=tops&sort=price-asc&page=2&pageSize=20`,
    );
  });

  it('fetchProducts omits unset filters', async () => {
    const spy = spyFetch();

    await fetchProducts({ page: 1, pageSize: 20 });

    expect(String(spy.mock.calls[0][0])).toBe(`${BASE}/api/v1/products?page=1&pageSize=20`);
  });

  it('fetchProduct URI-encodes the slug', async () => {
    const spy = spyFetch();

    await fetchProduct('blue-shirt');
    await fetchProduct('a/b?c');

    expect(String(spy.mock.calls[0][0])).toBe(`${BASE}/api/v1/products/blue-shirt`);
    expect(String(spy.mock.calls[1][0])).toBe(`${BASE}/api/v1/products/a%2Fb%3Fc`);
  });

  it('fetchCategories calls the categories endpoint', async () => {
    const spy = spyFetch();

    await fetchCategories();

    expect(String(spy.mock.calls[0][0])).toBe(`${BASE}/api/v1/categories`);
  });
});
