import { fireEvent, screen, waitFor } from 'expo-router/testing-library';
import { Text } from 'react-native';

import { makeAdminProduct } from '@/tests/admin-fixtures';
import { jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { createTestQueryClient, renderRouterWithQuery } from '@/tests/render-with-query';

import { AdminProductCreateView } from './AdminProductCreateView';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const render = (client = createTestQueryClient()) =>
  renderRouterWithQuery(
    { 'admin/products/new': AdminProductCreateView, 'admin/products/[id]': () => <Text>Detail screen</Text> },
    { initialUrl: '/admin/products/new', client },
  );

async function fillValid() {
  await fireEvent.changeText(screen.getByLabelText('Name'), ' Linen shirt ');
  await fireEvent.changeText(screen.getByLabelText('Slug'), 'linen-shirt');
  await fireEvent.changeText(screen.getByLabelText('Category slug'), 'shirts');
  await fireEvent.changeText(screen.getByLabelText('Variant 1 SKU'), 'LS-1');
  await fireEvent.changeText(screen.getByLabelText('Variant 1 name'), 'Default');
  await fireEvent.changeText(screen.getByLabelText('Variant 1 price'), '49.00');
}

describe('AdminProductCreateView', () => {
  it('sends prices exactly as typed, never sends isActive, and invalidates the admin and public lists', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(201, makeAdminProduct()));
    const client = createTestQueryClient();
    const invalidate = jest.spyOn(client, 'invalidateQueries');
    await render(client);

    await fillValid();
    await fireEvent.press(screen.getByRole('button', { name: 'Create product' }));

    await waitFor(() => expect(screen).toHavePathname('/admin/products/a-1'));
    const init = spy.mock.calls[0][1] as RequestInit;
    expect(init.method).toBe('POST');
    const body = JSON.parse(init.body as string);
    expect(body.name).toBe('Linen shirt');
    expect(body.variants[0].price).toEqual({ amount: '49.00', currency: 'USD' });
    expect(body.variants[0]).not.toHaveProperty('isActive');
    expect(body).not.toHaveProperty('isActive');
    const keys = invalidate.mock.calls.map((c) => JSON.stringify((c[0] as { queryKey: unknown }).queryKey));
    expect(keys).toEqual(expect.arrayContaining([JSON.stringify(['admin', 'products']), JSON.stringify(['products'])]));
  });

  it('keeps a price like 1.50 as the text 1.50', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(201, makeAdminProduct()));
    await render();

    await fillValid();
    await fireEvent.changeText(screen.getByLabelText('Variant 1 price'), '1.50');
    await fireEvent.press(screen.getByRole('button', { name: 'Create product' }));

    await waitFor(() => expect(spy).toHaveBeenCalled());
    expect(JSON.parse((spy.mock.calls[0][1] as RequestInit).body as string).variants[0].price.amount).toBe('1.50');
  });

  it('shows the server field errors next to the fields on a 400, and stays on the form', async () => {
    jest
      .spyOn(globalThis, 'fetch')
      .mockImplementation(async () =>
        problemResponse(400, { errors: { name: ['Name is required.'], 'variants[0].price.amount': ['Price is not valid.'] } }),
      );
    await render();

    await fillValid();
    await fireEvent.press(screen.getByRole('button', { name: 'Create product' }));

    expect(await screen.findByText('Name is required.')).toBeOnTheScreen();
    expect(screen.getByText('Price is not valid.')).toBeOnTheScreen();
    expect(screen).toHavePathname('/admin/products/new');
  });

  it('shows a duplicate slug conflict on the slug field', async () => {
    jest
      .spyOn(globalThis, 'fetch')
      .mockImplementation(async () => problemResponse(409, { errors: { slug: ['A product with this slug already exists.'] } }));
    await render();

    await fillValid();
    await fireEvent.press(screen.getByRole('button', { name: 'Create product' }));

    expect(await screen.findByText('A product with this slug already exists.')).toBeOnTheScreen();
  });

  it('can add and remove variant rows', async () => {
    await render();

    await fireEvent.press(screen.getByRole('button', { name: 'Add variant' }));
    expect(screen.getByLabelText('Variant 2 SKU')).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: 'Remove variant 2' }));
    expect(screen.queryByLabelText('Variant 2 SKU')).toBeNull();
  });

  it('cannot add images before the product exists: Add image is disabled with a hint', async () => {
    await render();

    expect(screen.getByRole('button', { name: 'Add image' })).toBeDisabled();
    expect(screen.getByText('Save the product first to add images.')).toBeOnTheScreen();
  });
});
