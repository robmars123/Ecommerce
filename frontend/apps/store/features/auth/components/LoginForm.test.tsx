import { fireEvent, screen, waitFor } from 'expo-router/testing-library';
import { Text } from 'react-native';

import { getSessionSnapshot, resetSession } from '@/features/auth/session';
import { jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { renderRouterWithQuery } from '@/tests/render-with-query';
import { makeTokens } from '@/tests/session';

import { LOGIN_FAILED_COPY, LoginForm, PASSWORD_CHANGED_COPY } from './LoginForm';

jest.mock('expo-secure-store');

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const render = (url: string) =>
  renderRouterWithQuery(
    {
      index: () => <Text>Home screen</Text>,
      login: LoginForm,
      'admin/products': () => <Text>Admin products screen</Text>,
    },
    { initialUrl: url },
  );

async function fill() {
  await fireEvent.changeText(screen.getByLabelText('Email'), 'ana@example.test');
  await fireEvent.changeText(screen.getByLabelText('Password'), 'correct horse battery');
}

describe('LoginForm', () => {
  it('has labelled fields and hides the password', async () => {
    await render('/login');

    expect(screen.getByLabelText('Email')).toBeOnTheScreen();
    expect(screen.getByLabelText('Password').props.secureTextEntry).toBe(true);
  });

  it('signs in and goes to a safe returnTo', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeTokens('Admin')));
    await render('/login?returnTo=%2Fadmin%2Fproducts');

    await fill();
    await fireEvent.press(screen.getByRole('button', { name: 'Log in' }));

    await waitFor(() => expect(screen).toHavePathname('/admin/products'));
    expect(getSessionSnapshot().status).toBe('signed-in');
    expect(JSON.parse((spy.mock.calls[0][1] as RequestInit).body as string)).toEqual({
      email: 'ana@example.test',
      password: 'correct horse battery',
    });
  });

  it('ignores an unsafe returnTo', async () => {
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(200, makeTokens('Customer')));
    await render('/login?returnTo=%2F%2Fevil.test');

    await fill();
    await fireEvent.press(screen.getByRole('button', { name: 'Log in' }));

    await waitFor(() => expect(screen).toHavePathname('/'));
  });

  it('shows one generic message for a failed login and stays signed out', async () => {
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => problemResponse(401, { title: 'Login failed' }));
    await render('/login');

    await fill();
    await fireEvent.press(screen.getByRole('button', { name: 'Log in' }));

    expect(await screen.findByText(LOGIN_FAILED_COPY)).toBeOnTheScreen();
    expect(screen).toHavePathname('/login');
  });

  it('disables the button while the request is in flight', async () => {
    let release: (r: Response) => void = () => undefined;
    jest.spyOn(globalThis, 'fetch').mockImplementation(() => new Promise<Response>((resolve) => (release = resolve)));
    await render('/login');

    await fill();
    await fireEvent.press(screen.getByRole('button', { name: 'Log in' }));

    await waitFor(() => expect(screen.getByRole('button', { name: 'Log in' })).toBeDisabled());
    release(jsonResponse(200, makeTokens('Customer')));
  });

  it('shows the password-changed notice', async () => {
    await render('/login?notice=password-changed');

    expect(screen.getByText(PASSWORD_CHANGED_COPY)).toBeOnTheScreen();
  });
});
