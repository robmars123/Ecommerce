import { fireEvent, screen } from 'expo-router/testing-library';
import { Text } from 'react-native';

import ProductDetailScreen from '@/app/products/[slug]';
import { jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { makeDetail, makeVariant } from '@/tests/fixtures';
import { renderRouterWithQuery } from '@/tests/render-with-query';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

function mockDetail(response: () => Response) {
  return jest.spyOn(globalThis, 'fetch').mockImplementation(async () => response());
}

const render = (slug = 'blue-shirt') =>
  renderRouterWithQuery(
    { 'products/[slug]': ProductDetailScreen, 'products/index': () => <Text>List screen</Text> },
    { initialUrl: `/products/${slug}` },
  );

const detail = makeDetail({
  images: [
    { url: null, altText: 'Blue shirt, front' },
    { url: null, altText: 'Blue shirt, back' },
  ],
  variants: [
    makeVariant({ id: 'v-s', sku: 'BS-S', name: 'Small', stockStatus: 'Out' }),
    makeVariant({
      id: 'v-m',
      sku: 'BS-M',
      name: 'Medium',
      price: { amount: '26.00', currency: 'USD' },
      compareAtPrice: { amount: '30.00', currency: 'USD' },
      stockStatus: 'Low',
    }),
  ],
});

describe('ProductDetailView', () => {
  it('renders name header, category, description, images and the variants', async () => {
    mockDetail(() => jsonResponse(200, detail));
    await render();

    expect(await screen.findAllByRole('header', { name: 'Blue Shirt' })).not.toHaveLength(0);
    expect(screen.getByText('Tops')).toBeOnTheScreen();
    expect(screen.getByText('A soft blue shirt.')).toBeOnTheScreen();
    expect(screen.getAllByLabelText('Blue shirt, front').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByRole('button', { name: 'Small' })).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Medium' })).toBeOnTheScreen();
  });

  it('shows Product as the header while loading', async () => {
    mockDetail(() => jsonResponse(200, detail));
    await render();

    expect(screen.getAllByRole('header', { name: 'Product' }).length).toBeGreaterThanOrEqual(1);
    expect(screen.getByLabelText('Loading')).toBeOnTheScreen();
    await screen.findAllByRole('header', { name: 'Blue Shirt' });
  });

  it('defaults to the first variant and switches price and stock badge when another is selected', async () => {
    mockDetail(() => jsonResponse(200, detail));
    await render();
    await screen.findByRole('button', { name: 'Small' });

    expect(screen.getByRole('button', { name: 'Small' })).toBeSelected();
    expect(screen.getByText('Out of stock')).toBeOnTheScreen();
    expect(screen.getByText('$24.50')).toBeOnTheScreen();

    await fireEvent.press(screen.getByRole('button', { name: 'Medium' }));

    expect(screen.getByRole('button', { name: 'Medium' })).toBeSelected();
    expect(screen.getByText('$26.00')).toBeOnTheScreen();
    expect(screen.getByLabelText('Was $30.00')).toBeOnTheScreen();
    expect(screen.getByText('Low stock')).toBeOnTheScreen();
    expect(screen.queryByText('Out of stock')).toBeNull();
  });

  it('shows no stock badge when the status is Unknown', async () => {
    mockDetail(() => jsonResponse(200, makeDetail()));
    await render();
    await screen.findAllByRole('header', { name: 'Blue Shirt' });

    expect(screen.queryByText(/stock/i)).toBeNull();
  });

  it('switches the main image when a thumbnail is pressed', async () => {
    mockDetail(() => jsonResponse(200, detail));
    await render();
    await screen.findByRole('button', { name: 'Show image 2: Blue shirt, back' });
    expect(screen.getAllByLabelText('Blue shirt, back')).toHaveLength(1);

    await fireEvent.press(screen.getByRole('button', { name: 'Show image 2: Blue shirt, back' }));

    expect(screen.getAllByLabelText('Blue shirt, back')).toHaveLength(2);
    expect(screen.getByRole('button', { name: 'Show image 2: Blue shirt, back' })).toBeSelected();
  });

  it('offers an add-to-cart control for the selected variant (replaces the Slice 1a "no add-to-cart" test: app context 4.3)', async () => {
    mockDetail(() => jsonResponse(200, detail));
    await render();
    await screen.findAllByRole('header', { name: 'Blue Shirt' });

    expect(screen.getByRole('button', { name: 'Add to cart' })).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Increase quantity' })).toBeOnTheScreen();
  });

  it('a 404 shows Product not found with a link to browse, and no Retry', async () => {
    mockDetail(() => problemResponse(404, { title: 'Product not found' }));
    await render('nope');

    expect(await screen.findByText('Product not found')).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Try again' })).toBeNull();

    await fireEvent.press(screen.getByRole('link', { name: 'Browse products' }));

    expect(screen).toHavePathname('/products');
    expect(screen.getByText('List screen')).toBeOnTheScreen();
  });

  it('a 500 shows a plain-language error with Retry, and Retry fetches again', async () => {
    let fail = true;
    const spy = mockDetail(() => (fail ? problemResponse(500, {}) : jsonResponse(200, detail)));
    await render();

    expect(await screen.findByText('Something went wrong on our side. Please try again.')).toBeOnTheScreen();
    fail = false;
    await fireEvent.press(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findAllByRole('header', { name: 'Blue Shirt' })).not.toHaveLength(0);
    expect(spy).toHaveBeenCalledTimes(2);
  });
});

describe('ProductDetailView navigation and variant details', () => {
  it('links back to the shop, also when the product is not found', async () => {
    mockDetail(() => jsonResponse(200, detail));
    await render();

    await fireEvent.press(await screen.findByRole('link', { name: /Back to shop/ }));
    expect(screen).toHavePathname('/products');
  });

  it('offers the way back on the not-found state', async () => {
    mockDetail(() => problemResponse(404, { title: 'Product not found' }));
    await render('nope');

    expect(await screen.findByRole('link', { name: /Back to shop/ })).toBeOnTheScreen();
  });

  it('shows the selected option and its SKU, updating on selection, with no Save or rating text', async () => {
    mockDetail(() => jsonResponse(200, detail));
    await render();

    expect(await screen.findByText('Size: Small')).toBeOnTheScreen();
    expect(screen.getByText('BS-S')).toBeOnTheScreen();

    await fireEvent.press(screen.getByRole('button', { name: 'Medium' }));

    expect(screen.getByText('Size: Medium')).toBeOnTheScreen();
    expect(screen.getByText('BS-M')).toBeOnTheScreen();
    expect(screen.queryByText('BS-S')).toBeNull();
    expect(screen.queryByText(/save|rating|review/i)).toBeNull();
  });
});
