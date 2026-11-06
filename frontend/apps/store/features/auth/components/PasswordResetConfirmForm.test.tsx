import { fireEvent, screen, waitFor } from 'expo-router/testing-library';
import { Text } from 'react-native';

import { problemResponse } from '@/tests/fetch-mock';
import { renderRouterWithQuery } from '@/tests/render-with-query';

import { PASSWORD_CHANGED_COPY } from './LoginForm';
import { PasswordResetConfirmForm, RESET_LINK_INVALID_COPY } from './PasswordResetConfirmForm';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const render = () =>
  renderRouterWithQuery(
    {
      index: () => <PasswordResetConfirmForm token="reset-token-value" />,
      login: () => <Text>Login screen</Text>,
      'reset-password': () => <Text>Request screen</Text>,
    },
    { initialUrl: '/' },
  );

describe('PasswordResetConfirmForm', () => {
  it('sends the token and new password, then goes to login with a notice param', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => new Response(null, { status: 204 }));
    await render();

    await fireEvent.changeText(screen.getByLabelText('New password'), 'a brand new passphrase');
    await fireEvent.press(screen.getByRole('button', { name: 'Change password' }));

    await waitFor(() => expect(screen).toHavePathname('/login'));
    expect(screen).toHaveSearchParams({ notice: 'password-changed' });
    expect(JSON.parse((spy.mock.calls[0][1] as RequestInit).body as string)).toEqual({
      token: 'reset-token-value',
      newPassword: 'a brand new passphrase',
    });
    expect(PASSWORD_CHANGED_COPY).toBe('Your password was changed. Please log in.');
  });

  it('shows the invalid-link message with a way to ask again on a 400', async () => {
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => problemResponse(400, { title: 'Reset link not valid' }));
    await render();

    await fireEvent.changeText(screen.getByLabelText('New password'), 'a brand new passphrase');
    await fireEvent.press(screen.getByRole('button', { name: 'Change password' }));

    expect(await screen.findByText(RESET_LINK_INVALID_COPY)).toBeOnTheScreen();
    expect(screen.getByRole('link', { name: 'Request a new link' })).toBeOnTheScreen();
  });
});
