import { fireEvent, screen, waitFor } from 'expo-router/testing-library';
import { Text } from 'react-native';

import { getSessionSnapshot, resetSession } from '@/features/auth/session';
import { jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { renderRouterWithQuery } from '@/tests/render-with-query';
import { makeTokens } from '@/tests/session';

import { EMAIL_TAKEN_COPY, RegisterForm } from './RegisterForm';

jest.mock('expo-secure-store');

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  resetSession();
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

const render = () =>
  renderRouterWithQuery(
    { index: () => <Text>Home screen</Text>, register: RegisterForm, login: () => <Text>Login screen</Text> },
    { initialUrl: '/register' },
  );

async function fill() {
  await fireEvent.changeText(screen.getByLabelText('Name'), 'Ana');
  await fireEvent.changeText(screen.getByLabelText('Email'), 'ana@example.test');
  await fireEvent.changeText(screen.getByLabelText('Password'), 'correct horse battery');
}

describe('RegisterForm', () => {
  it('registers, signs in and goes home', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(201, makeTokens('Customer')));
    await render();

    await fill();
    await fireEvent.press(screen.getByRole('button', { name: 'Register' }));

    await waitFor(() => expect(screen).toHavePathname('/'));
    expect(getSessionSnapshot().status).toBe('signed-in');
    expect(JSON.parse((spy.mock.calls[0][1] as RequestInit).body as string)).toEqual({
      displayName: 'Ana',
      email: 'ana@example.test',
      password: 'correct horse battery',
    });
  });

  it('says the email is taken on a 409', async () => {
    jest.spyOn(globalThis, 'fetch').mockImplementation(async () => problemResponse(409, { title: 'Account already exists' }));
    await render();

    await fill();
    await fireEvent.press(screen.getByRole('button', { name: 'Register' }));

    expect(await screen.findByText(EMAIL_TAKEN_COPY)).toBeOnTheScreen();
  });

  it('shows the server field errors under the fields', async () => {
    jest
      .spyOn(globalThis, 'fetch')
      .mockImplementation(async () => problemResponse(400, { errors: { password: ['Password must be at least 8 characters.'] } }));
    await render();

    await fill();
    await fireEvent.press(screen.getByRole('button', { name: 'Register' }));

    expect(await screen.findByText('Password must be at least 8 characters.')).toBeOnTheScreen();
  });
});
