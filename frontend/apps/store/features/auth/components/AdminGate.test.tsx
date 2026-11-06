import { screen, waitFor } from 'expo-router/testing-library';
import { Text } from 'react-native';

import { resetSession } from '@/features/auth/session';
import { renderRouterWithQuery } from '@/tests/render-with-query';
import { signInAs } from '@/tests/session';

import { AdminGate } from './AdminGate';

jest.mock('expo-secure-store');

afterEach(() => resetSession());

const render = (url: string) =>
  renderRouterWithQuery(
    {
      index: () => <Text>Home screen</Text>,
      login: () => <Text>Login screen</Text>,
      'admin/index': () => <Text>Admin home</Text>,
      'admin/products': () => (
        <AdminGate>
          <Text>Secret products</Text>
        </AdminGate>
      ),
    },
    { initialUrl: url },
  );

describe('AdminGate', () => {
  it('renders nothing but a loading state while restoring', async () => {
    await render('/admin/products');

    expect(screen.getByLabelText('Loading')).toBeOnTheScreen();
    expect(screen.queryByText('Secret products')).toBeNull();
  });

  it('shows the page to an Admin', async () => {
    await signInAs('Admin');
    await render('/admin/products');

    expect(screen.getByText('Secret products')).toBeOnTheScreen();
  });

  it('sends a Support user to /admin', async () => {
    await signInAs('Support');
    await render('/admin/products');
    await waitFor(() => expect(screen).toHavePathname('/admin'));
    expect(screen.queryByText('Secret products')).toBeNull();
  });
});
