import { jsonResponse } from '@/tests/fetch-mock';
import { makeAdminProduct } from '@/tests/admin-fixtures';

import {
  createCategory,
  createProduct,
  deleteCategory,
  fetchAdminPreview,
  fetchAdminProduct,
  fetchAdminProducts,
  publishProduct,
  updateCategory,
  updateProduct,
  uploadProductImage,
} from './api';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

function lastCall(spy: jest.SpyInstance) {
  const [url, init] = spy.mock.calls.at(-1) as [string, RequestInit];
  return { url, method: init.method, body: init.body as string | undefined };
}

describe('admin catalog api', () => {
  it('uses the right method and path for each call', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeAdminProduct()));
    const empty = { slug: 's', name: 'n', description: '', categorySlug: 'c', variants: [], images: [] };

    await fetchAdminProducts({ page: 2, pageSize: 20, status: 'active', search: 'a b' });
    expect(lastCall(spy).url).toBe('http://api.test.invalid:5080/api/v1/admin/products?search=a%20b&status=active&page=2&pageSize=20');
    await fetchAdminProduct('p 1');
    expect(lastCall(spy).url).toContain('/api/v1/admin/products/p%201');
    await fetchAdminPreview('p1');
    expect(lastCall(spy).url).toContain('/p1/preview');
    await createProduct(empty);
    expect(lastCall(spy)).toMatchObject({ method: 'POST', url: expect.stringMatching(/\/api\/v1\/admin\/products$/) });
    await updateProduct('p1', { ...empty, version: 5 });
    expect(lastCall(spy)).toMatchObject({ method: 'PUT', url: expect.stringContaining('/p1') });
    expect(JSON.parse(lastCall(spy).body!).version).toBe(5);
    await publishProduct('p1', true);
    expect(lastCall(spy)).toMatchObject({ method: 'POST', url: expect.stringContaining('/p1/publish') });
    await publishProduct('p1', false);
    expect(lastCall(spy).url).toContain('/p1/unpublish');
    await createCategory({ slug: 'a', name: 'A', parentSlug: null });
    expect(lastCall(spy)).toMatchObject({ method: 'POST', url: expect.stringContaining('/api/v1/admin/categories') });
  });

  it('updates a category with exactly { slug, name } and encodes the current slug', async () => {
    const spy = jest
      .spyOn(globalThis, 'fetch')
      .mockImplementation(async () => jsonResponse(200, { slug: 'shirts', name: 'Shirts', parentSlug: 'clothing' }));
    await updateCategory('tops', { slug: 'shirts', name: 'Shirts' });
    const first = lastCall(spy);
    expect(first).toMatchObject({ method: 'PUT', url: expect.stringMatching(/\/api\/v1\/admin\/categories\/tops$/) });
    expect(JSON.parse(first.body as string)).toEqual({ slug: 'shirts', name: 'Shirts' });
    expect(first.body).not.toContain('parentSlug');

    await updateCategory('a b/c', { slug: 'x', name: 'X' });
    expect(lastCall(spy).url).toMatch(/\/categories\/a%20b%2Fc$/);
  });

  it('deletes a category and resolves on 204', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => new Response(null, { status: 204 }));
    await expect(deleteCategory('tops')).resolves.toBeDefined();
    expect(lastCall(spy)).toMatchObject({ method: 'DELETE', url: expect.stringMatching(/\/api\/v1\/admin\/categories\/tops$/) });
  });
});

describe('uploadProductImage', () => {
  it('POSTs the multipart form to the product images route and returns the key and url', async () => {
    const spy = jest
      .spyOn(globalThis, 'fetch')
      .mockImplementation(async () => jsonResponse(200, { storageKey: 'products/a-1/x.png', url: '/api/v1/images/products/a-1/x.png' }));
    const form = new FormData();
    form.append('file', new Blob(['x']), 'a.png');

    const result = await uploadProductImage('a-1', form);

    const [url, init] = spy.mock.calls[0] as [string, RequestInit];
    expect(url).toBe('http://api.test.invalid:5080/api/v1/admin/products/a-1/images');
    expect(init.method).toBe('POST');
    expect(init.body).toBe(form);
    expect(result.url).toBe('/api/v1/images/products/a-1/x.png');
  });
});
