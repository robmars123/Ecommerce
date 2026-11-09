import { fireEvent, screen, waitFor } from 'expo-router/testing-library';

import { categories } from '@/tests/fixtures';
import { jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { createTestQueryClient, renderRouterWithQuery } from '@/tests/render-with-query';

import { AdminCategoriesView } from './AdminCategoriesView';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

type Handler = (method: string, pathname: string) => Response | Promise<Response> | undefined;

/** GET /categories returns the fixture; the handler answers the admin PUT/DELETE calls. */
function mockApi(handler: Handler = () => undefined, items = categories) {
  return jest.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const { pathname } = new URL(String(input));
    const method = init?.method ?? 'GET';
    if (method === 'GET' && pathname === '/api/v1/categories') return jsonResponse(200, { items });
    return (await handler(method, pathname)) ?? new Response('not mocked', { status: 599 });
  });
}

const adminCalls = (spy: jest.SpyInstance) =>
  spy.mock.calls.filter((c) => (c[1] as RequestInit | undefined)?.method && (c[1] as RequestInit).method !== 'GET');

async function render(client = createTestQueryClient()) {
  await renderRouterWithQuery({ index: AdminCategoriesView }, { initialUrl: '/', client });
}

const nameField = () => screen.getByLabelText('New name for Tops');
const slugField = () => screen.getByLabelText('New slug for Tops');

describe('AdminCategoriesView', () => {
  it('lists the categories with their parent', async () => {
    mockApi();
    await render();

    expect(await screen.findByText('Admin · Categories')).toBeOnTheScreen();
    expect(await screen.findByText('Clothing · clothing')).toBeOnTheScreen();
    expect(screen.getByText('Top level')).toBeOnTheScreen();
    expect(screen.getByText('Tops · tops')).toBeOnTheScreen();
    expect(screen.getByText('Under Clothing')).toBeOnTheScreen();
  });

  it('shows an empty state', async () => {
    mockApi(() => undefined, []);
    await render();

    expect(await screen.findByText('No categories yet.')).toBeOnTheScreen();
  });

  it('shows an error with a retry that refetches', async () => {
    let fail = true;
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => (fail ? problemResponse(500, {}) : jsonResponse(200, { items: categories })));
    await render();

    const retry = await screen.findByRole('button', { name: 'Try again' });
    const before = spy.mock.calls.length;
    fail = false;
    await fireEvent.press(retry);

    expect(await screen.findByText('Tops · tops')).toBeOnTheScreen();
    expect(spy.mock.calls.length).toBeGreaterThan(before);
  });

  it('has the create form on the screen', async () => {
    mockApi();
    await render();

    expect(await screen.findByLabelText('Category name')).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Add category' })).toBeOnTheScreen();
  });

  it('edits a category with { slug, name } only and refetches categories and product lists', async () => {
    const spy = mockApi((method) => (method === 'PUT' ? jsonResponse(200, { slug: 'shirts', name: 'Shirts', parentSlug: 'clothing' }) : undefined));
    const client = createTestQueryClient();
    const invalidate = jest.spyOn(client, 'invalidateQueries');
    await render(client);

    await fireEvent.press(await screen.findByRole('button', { name: 'Edit Tops' }));
    expect(nameField().props.value).toBe('Tops');
    expect(slugField().props.value).toBe('tops');
    await fireEvent.changeText(nameField(), ' Shirts ');
    await fireEvent.changeText(slugField(), 'shirts');
    await fireEvent.press(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(screen.queryByRole('button', { name: 'Save' })).toBeNull());
    const calls = adminCalls(spy);
    expect(calls).toHaveLength(1);
    expect(String(calls[0][0])).toMatch(/\/api\/v1\/admin\/categories\/tops$/);
    expect((calls[0][1] as RequestInit).method).toBe('PUT');
    const body = JSON.parse((calls[0][1] as RequestInit).body as string);
    expect(body).toEqual({ slug: 'shirts', name: 'Shirts' });
    expect(body).not.toHaveProperty('parentSlug');
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['categories'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['admin', 'products'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['products'] });
  });

  it('shows a taken slug under the slug field and keeps the editor open with the typed values', async () => {
    mockApi((method) => (method === 'PUT' ? problemResponse(409, { errors: { slug: ['That slug is already used.'] } }) : undefined));
    await render();

    await fireEvent.press(await screen.findByRole('button', { name: 'Edit Tops' }));
    await fireEvent.changeText(slugField(), 'clothing');
    await fireEvent.press(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByText('That slug is already used.')).toBeOnTheScreen();
    expect(slugField().props.value).toBe('clothing');
    expect(screen.getByRole('button', { name: 'Save' })).toBeOnTheScreen();
  });

  it('cancels an edit without a request and shows no stale error on reopen', async () => {
    const spy = mockApi((method) => (method === 'PUT' ? problemResponse(409, { errors: { slug: ['That slug is already used.'] } }) : undefined));
    await render();

    await fireEvent.press(await screen.findByRole('button', { name: 'Edit Tops' }));
    await fireEvent.press(screen.getByRole('button', { name: 'Save' }));
    await screen.findByText('That slug is already used.');
    const before = adminCalls(spy).length;
    await fireEvent.press(screen.getByRole('button', { name: 'Cancel' }));

    expect(screen.queryByRole('button', { name: 'Save' })).toBeNull();
    await fireEvent.press(screen.getByRole('button', { name: 'Edit Tops' }));
    expect(screen.queryByText('That slug is already used.')).toBeNull();
    expect(adminCalls(spy)).toHaveLength(before);
  });

  it('needs a confirm before deleting, then deletes and refetches categories', async () => {
    const spy = mockApi((method) => (method === 'DELETE' ? new Response(null, { status: 204 }) : undefined));
    const client = createTestQueryClient();
    const invalidate = jest.spyOn(client, 'invalidateQueries');
    await render(client);

    await fireEvent.press(await screen.findByRole('button', { name: 'Delete Tops' }));
    expect(screen.getByText('Delete Tops? Products and subcategories must be moved first.')).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Confirm delete' })).toBeOnTheScreen();
    expect(adminCalls(spy)).toHaveLength(0);

    await fireEvent.press(screen.getByRole('button', { name: 'Cancel' }));
    expect(screen.queryByRole('button', { name: 'Confirm delete' })).toBeNull();
    expect(adminCalls(spy)).toHaveLength(0);

    await fireEvent.press(screen.getByRole('button', { name: 'Delete Tops' }));
    await fireEvent.press(screen.getByRole('button', { name: 'Confirm delete' }));

    await waitFor(() => expect(adminCalls(spy)).toHaveLength(1));
    expect(String(adminCalls(spy)[0][0])).toMatch(/\/api\/v1\/admin\/categories\/tops$/);
    expect((adminCalls(spy)[0][1] as RequestInit).method).toBe('DELETE');
    await waitFor(() => expect(invalidate).toHaveBeenCalledWith({ queryKey: ['categories'] }));
  });

  it.each([
    ['Tops', 'products', 'This category still has products. Move them to another category first.'],
    ['Clothing', 'subcategories', 'This category still has subcategories. Move or delete them first.'],
  ])('shows the server message when deleting %s is refused (%s)', async (name, key, message) => {
    mockApi((method) => (method === 'DELETE' ? problemResponse(409, { errors: { [key]: [message] } }) : undefined));
    await render();

    await fireEvent.press(await screen.findByRole('button', { name: `Delete ${name}` }));
    await fireEvent.press(screen.getByRole('button', { name: 'Confirm delete' }));

    expect(await screen.findByText(message)).toBeOnTheScreen();
    expect(screen.getByRole('alert')).toHaveTextContent(message);
    expect(screen.getByRole('button', { name: 'Confirm delete' })).toBeOnTheScreen();
    expect(screen.getByText(`${name} · ${name.toLowerCase()}`)).toBeOnTheScreen();
  });

  it('shows the not-found text on a 404 and refetches categories', async () => {
    mockApi((method) => (method === 'DELETE' ? problemResponse(404, {}) : undefined));
    const client = createTestQueryClient();
    const invalidate = jest.spyOn(client, 'invalidateQueries');
    await render(client);

    await fireEvent.press(await screen.findByRole('button', { name: 'Delete Tops' }));
    await fireEvent.press(screen.getByRole('button', { name: 'Confirm delete' }));

    expect(await screen.findByRole('alert')).toHaveTextContent("We couldn't find that.");
    await waitFor(() => expect(invalidate).toHaveBeenCalledWith({ queryKey: ['categories'] }));
  });

  it('keeps one row open at a time', async () => {
    mockApi();
    await render();

    await fireEvent.press(await screen.findByRole('button', { name: 'Edit Tops' }));
    expect(screen.getByRole('button', { name: 'Save' })).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: 'Delete Clothing' }));

    expect(screen.queryByRole('button', { name: 'Save' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Confirm delete' })).toBeOnTheScreen();
  });

  it('disables Save while the request is pending so a second press sends nothing', async () => {
    let release: (r: Response) => void = () => undefined;
    const pending = new Promise<Response>((resolve) => {
      release = resolve;
    });
    const spy = mockApi((method) => (method === 'PUT' ? pending : undefined));
    await render();

    await fireEvent.press(await screen.findByRole('button', { name: 'Edit Tops' }));
    await fireEvent.press(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled());
    await fireEvent.press(screen.getByRole('button', { name: 'Save' }));
    expect(adminCalls(spy)).toHaveLength(1);
    release(jsonResponse(200, { slug: 'tops', name: 'Tops', parentSlug: 'clothing' }));
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Save' })).toBeNull());
  });

  it.each([
    ['delete', 'DELETE', 'Delete Tops', 'Confirm delete'],
    ['rename', 'PUT', 'Edit Tops', 'Save'],
  ])('does not reopen the row when a slug comes back after a %s', async (kind, verb, openName, openMarker) => {
    let current = categories;
    jest.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const { pathname } = new URL(String(input));
      const method = init?.method ?? 'GET';
      if (method === 'GET' && pathname === '/api/v1/categories') return jsonResponse(200, { items: current });
      if (method === verb) {
        current = categories.filter((c) => c.slug !== 'tops');
        return kind === 'delete' ? new Response(null, { status: 204 }) : jsonResponse(200, { slug: 'shirts', name: 'Shirts', parentSlug: 'clothing' });
      }
      return new Response('not mocked', { status: 599 });
    });
    const client = createTestQueryClient();
    await render(client);

    await fireEvent.press(await screen.findByRole('button', { name: openName }));
    if (kind === 'rename') await fireEvent.changeText(slugField(), 'shirts');
    await fireEvent.press(screen.getByRole('button', { name: openMarker }));
    await waitFor(() => expect(screen.queryByText('Tops · tops')).toBeNull());

    current = categories;
    await client.invalidateQueries({ queryKey: ['categories'] });

    expect(await screen.findByText('Tops · tops')).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: openMarker })).toBeNull();
    expect(screen.getByRole('button', { name: 'Edit Tops' })).toBeOnTheScreen();
  });
});
