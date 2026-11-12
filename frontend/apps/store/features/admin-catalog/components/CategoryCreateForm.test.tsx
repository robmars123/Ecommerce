import { fireEvent, screen, waitFor } from 'expo-router/testing-library';

import { jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { createTestQueryClient, renderRouterWithQuery } from '@/tests/render-with-query';

import { CategoryCreateForm } from './CategoryCreateForm';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

async function fill(parent = '') {
  await fireEvent.changeText(screen.getByLabelText('Category name'), 'Tees');
  await fireEvent.changeText(screen.getByLabelText('Category slug'), 'tees');
  await fireEvent.changeText(screen.getByLabelText('Parent category slug'), parent);
}

describe('CategoryCreateForm', () => {
  it('creates a category, sends a null parent when blank, refetches categories and clears the form', async () => {
    const spy = jest
      .spyOn(globalThis, 'fetch')
      .mockImplementation(async () => jsonResponse(201, { slug: 'tees', name: 'Tees', parentSlug: null }));
    const client = createTestQueryClient();
    const invalidate = jest.spyOn(client, 'invalidateQueries');
    await renderRouterWithQuery({ index: CategoryCreateForm }, { initialUrl: '/', client });

    await fill();
    await fireEvent.press(screen.getByRole('button', { name: 'Add category' }));

    expect(await screen.findByText('Category created.')).toBeOnTheScreen();
    expect(JSON.parse((spy.mock.calls[0][1] as RequestInit).body as string)).toEqual({ name: 'Tees', slug: 'tees', parentSlug: null });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['categories'] });
    await waitFor(() => expect(screen.getByLabelText('Category name').props.value).toBe(''));
  });

  it('shows a bad parent next to the parent field', async () => {
    jest
      .spyOn(globalThis, 'fetch')
      .mockImplementation(async () => problemResponse(400, { errors: { parentSlug: ['That parent category does not exist.'] } }));
    await renderRouterWithQuery({ index: CategoryCreateForm }, { initialUrl: '/' });

    await fill('missing');
    await fireEvent.press(screen.getByRole('button', { name: 'Add category' }));

    expect(await screen.findByText('That parent category does not exist.')).toBeOnTheScreen();
  });
});
