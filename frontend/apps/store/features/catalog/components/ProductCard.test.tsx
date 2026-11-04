import { Stack } from 'expo-router';
import { fireEvent, renderRouter, screen } from 'expo-router/testing-library';
import { Text } from 'react-native';

import { makeItem } from '@/tests/fixtures';

import { ProductCard } from './ProductCard';

function routes(item = makeItem()) {
  return {
    _layout: () => <Stack screenOptions={{ headerShown: false }} />,
    index: () => <ProductCard item={item} />,
    'products/[slug]': () => <Text>Detail screen</Text>,
  };
}

describe('ProductCard', () => {
  it('is a link named by the product name', async () => {
    await renderRouter(routes(), { initialUrl: '/' });

    expect(screen.getByRole('link', { name: 'Blue Shirt' })).toBeOnTheScreen();
  });

  it('shows price, category, a labelled image placeholder and no badge for Unknown stock', async () => {
    await renderRouter(routes(), { initialUrl: '/' });

    expect(screen.getByText('$24.50')).toBeOnTheScreen();
    expect(screen.getByText('Tops')).toBeOnTheScreen();
    expect(screen.getByLabelText('Blue shirt, front')).toBeOnTheScreen();
    expect(screen.queryByText(/stock/i)).toBeNull();
  });

  it('shows the stock badge and compare-at price when present', async () => {
    const item = makeItem({ stockStatus: 'Low', compareAtPrice: { amount: '30.00', currency: 'USD' } });
    await renderRouter(routes(item), { initialUrl: '/' });

    expect(screen.getByText('Low stock')).toBeOnTheScreen();
    expect(screen.getByLabelText('Was $30.00')).toBeOnTheScreen();
  });

  it('labels the image placeholder with the product name when there is no image', async () => {
    await renderRouter(routes(makeItem({ primaryImage: null })), { initialUrl: '/' });

    expect(screen.getAllByLabelText('Blue Shirt').length).toBeGreaterThanOrEqual(1);
  });

  it('navigates to /products/{slug}', async () => {
    await renderRouter(routes(), { initialUrl: '/' });

    await fireEvent.press(screen.getByRole('link', { name: 'Blue Shirt' }));

    expect(screen).toHavePathname('/products/blue-shirt');
    expect(screen.getByText('Detail screen')).toBeOnTheScreen();
  });
});
