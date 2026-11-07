import { screen } from 'expo-router/testing-library';
import { Text } from 'react-native';

import IndexScreen from '@/app/index';

import { renderRouterWithQuery } from './render-with-query';

it('opens on Shop: "/" redirects to the product list', async () => {
  await renderRouterWithQuery(
    { index: IndexScreen, 'products/index': () => <Text>Shop screen</Text> },
    { initialUrl: '/' },
  );
  expect(await screen.findByText('Shop screen')).toBeOnTheScreen();
});
