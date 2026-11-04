import { Stack } from 'expo-router';
import { fireEvent, renderRouter, screen } from 'expo-router/testing-library';
import { Text } from 'react-native';

import { StubLink } from './StubLink';

const routes = {
  _layout: () => <Stack screenOptions={{ headerShown: false }} />,
  index: () => <StubLink href="/cart" label="Cart" />,
  cart: () => <Text>Cart screen</Text>,
};

describe('StubLink', () => {
  it('exposes an accessible link named by its label', async () => {
    await renderRouter(routes, { initialUrl: '/' });
    expect(screen.getByRole('link', { name: 'Cart' })).toBeOnTheScreen();
  });

  it('meets the 44pt minimum touch target', async () => {
    await renderRouter(routes, { initialUrl: '/' });
    expect(screen.getByRole('link', { name: 'Cart' })).toHaveStyle({ minHeight: 44 });
  });

  it('navigates to the href when pressed', async () => {
    await renderRouter(routes, { initialUrl: '/' });
    await fireEvent.press(screen.getByRole('link', { name: 'Cart' }));

    expect(screen).toHavePathname('/cart');
    expect(screen.getByText('Cart screen')).toBeOnTheScreen();
  });
});
