import { Stack } from 'expo-router';
import { renderRouter, screen } from 'expo-router/testing-library';
import { Text } from 'react-native';

import { ScreenStub } from './ScreenStub';

const layout = () => <Stack screenOptions={{ headerShown: false }} />;

describe('ScreenStub', () => {
  it('renders the title as an accessible header with the not-built note', async () => {
    await renderRouter({ _layout: layout, index: () => <ScreenStub title="Cart" /> }, { initialUrl: '/' });

    expect(screen.getByRole('header', { name: 'Cart' })).toBeOnTheScreen();
    expect(screen.getByText('Not built yet.')).toBeOnTheScreen();
  });

  it('renders detail only when given', async () => {
    await renderRouter({ _layout: layout, index: () => <ScreenStub title="Order" detail="Order id: 42" /> }, { initialUrl: '/' });
    expect(screen.getByText('Order id: 42')).toBeOnTheScreen();
  });

  it('renders no detail text when detail is omitted', async () => {
    await renderRouter({ _layout: layout, index: () => <ScreenStub title="Order" /> }, { initialUrl: '/' });
    expect(screen.queryByText('Order id: 42')).toBeNull();
    // Only the header and the note are rendered as text.
    expect(screen.getAllByText(/.+/)).toHaveLength(2);
  });

  it('renders children', async () => {
    await renderRouter(
      {
        _layout: layout,
        index: () => (
          <ScreenStub title="Cart">
            <Text>child</Text>
          </ScreenStub>
        ),
      },
      { initialUrl: '/' },
    );
    expect(screen.getByText('child')).toBeOnTheScreen();
  });
});
