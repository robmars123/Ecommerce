import { fireEvent, screen } from 'expo-router/testing-library';
import { Text } from 'react-native';

import HomeScreen from '@/app/index';
import { jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { categories } from '@/tests/fixtures';
import { renderRouterWithQuery } from '@/tests/render-with-query';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const render = () =>
  renderRouterWithQuery(
    { index: HomeScreen, 'products/index': () => <Text>List screen</Text> },
    { initialUrl: '/' },
  );

describe('HomeView', () => {
  it('shows the Home header, category links and a browse-all link', async () => {
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, { items: categories }));
    await render();

    expect(screen.getAllByRole('header', { name: 'Home' }).length).toBeGreaterThanOrEqual(1);
    expect(await screen.findByRole('link', { name: 'Tops' })).toBeOnTheScreen();
    expect(screen.getByRole('link', { name: 'Browse all products' })).toBeOnTheScreen();
  });

  it('submitting a search navigates to /products?search=...', async () => {
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, { items: categories }));
    await render();

    await fireEvent.changeText(screen.getByLabelText('Search products'), ' jeans ');
    await fireEvent(screen.getByLabelText('Search products'), 'submitEditing');

    expect(screen).toHavePathname('/products');
    expect(screen).toHaveSearchParams({ search: 'jeans' });
  });

  it('submitting an empty search browses everything', async () => {
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, { items: categories }));
    await render();

    await fireEvent(screen.getByLabelText('Search products'), 'submitEditing');

    expect(screen).toHavePathname('/products');
    expect(screen).toHaveSearchParams({});
  });

  it('a category link navigates to /products?category=...', async () => {
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, { items: categories }));
    await render();

    await fireEvent.press(await screen.findByRole('link', { name: 'Tops' }));

    expect(screen).toHavePathname('/products');
    expect(screen).toHaveSearchParams({ category: 'tops' });
  });

  it('Browse all products navigates to /products', async () => {
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, { items: categories }));
    await render();

    await fireEvent.press(screen.getByRole('link', { name: 'Browse all products' }));

    expect(screen).toHavePathname('/products');
  });

  it('shows a retryable error when categories fail to load', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => problemResponse(500, {}));
    await render();

    expect(await screen.findByText('Something went wrong on our side. Please try again.')).toBeOnTheScreen();
    spy.mockImplementation(async () => jsonResponse(200, { items: categories }));
    await fireEvent.press(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByRole('link', { name: 'Tops' })).toBeOnTheScreen();
  });
});
