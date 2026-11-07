import { QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import { fireEvent, renderRouter, screen } from 'expo-router/testing-library';
import { Text } from 'react-native';

import { endSession, resetSession } from '@/features/auth/session';
import { createTestQueryClient } from '@/tests/render-with-query';
import { signInAs } from '@/tests/session';

import { TopNav } from './TopNav';

jest.mock('expo-secure-store');

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

function routes() {
  const client = createTestQueryClient();
  return {
    _layout: () => (
      <QueryClientProvider client={client}>
        <Stack screenOptions={{ header: () => <TopNav /> }} />
      </QueryClientProvider>
    ),
    index: () => <Text>Home screen</Text>,
    products: () => <Text>Shop screen</Text>,
    orders: () => <Text>Orders screen</Text>,
    cart: () => <Text>Cart screen</Text>,
    login: () => <Text>Login screen</Text>,
    register: () => <Text>Register screen</Text>,
  };
}

describe('TopNav', () => {
  it.each([
    ['Northline.', '/'],
    ['Shop', '/products'],
    ['Orders', '/orders'],
    ['Cart', '/cart'],
  ])('%s is a link to %s', async (name, pathname) => {
    // Orders shows only for a signed-in customer, so every row is checked in that state.
    await signInAs('Customer');
    await renderRouter(routes(), { initialUrl: '/login' });

    // Signed in, AccountBar also lists an Orders link, so take the first match.
    await fireEvent.press(screen.getAllByRole('link', { name })[0]);

    expect(screen).toHavePathname(pathname);
  });

  it('marks Shop selected on /products and not on /cart', async () => {
    await renderRouter(routes(), { initialUrl: '/products' });
    expect(screen.getByRole('link', { name: 'Shop' })).toBeSelected();
    expect(screen.getByRole('link', { name: 'Cart' })).not.toBeSelected();

    await fireEvent.press(screen.getByRole('link', { name: 'Cart' }));
    expect(screen.getByRole('link', { name: 'Shop' })).not.toBeSelected();
  });

  it('shows Log in once when signed out', async () => {
    await renderRouter(routes(), { initialUrl: '/' });
    await endSession();

    expect(await screen.findAllByRole('link', { name: 'Log in' })).toHaveLength(1);
  });

  it('hides Orders from a guest', async () => {
    await renderRouter(routes(), { initialUrl: '/' });
    await endSession();

    expect(await screen.findAllByRole('link', { name: 'Log in' })).toHaveLength(1);
    expect(screen.queryByRole('link', { name: 'Orders' })).toBeNull();
  });

  it('shows Orders to a signed-in customer', async () => {
    await signInAs('Customer');
    await renderRouter(routes(), { initialUrl: '/' });

    expect(screen.getAllByRole('link', { name: 'Orders' }).length).toBeGreaterThanOrEqual(1);
  });

  it('hides Orders while the session is still restoring', async () => {
    await renderRouter(routes(), { initialUrl: '/' });

    expect(screen.queryByRole('link', { name: 'Orders' })).toBeNull();
  });
});
