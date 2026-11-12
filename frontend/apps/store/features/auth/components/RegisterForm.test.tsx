import { fireEvent, screen, waitFor } from 'expo-router/testing-library';
import { Text } from 'react-native';

import { getSessionSnapshot, resetSession } from '@/features/auth/session';
import { jsonResponse, problemResponse } from '@/tests/fetch-mock';
import { renderRouterWithQuery } from '@/tests/render-with-query';
import { makeTokens } from '@/tests/session';
import { useGuestCartStore } from '@/features/cart/guest-cart-store';

import { EMAIL_TAKEN_COPY, PASSWORD_MISMATCH_COPY, RegisterForm } from './RegisterForm';

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
  await fireEvent.changeText(screen.getByLabelText('Confirm password'), 'correct horse battery');
}

describe('RegisterForm', () => {
  it('registers, signs in and goes home', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(201, makeTokens('Customer')));
    await render();

    await fill();
    await fireEvent.press(screen.getByRole('button', { name: 'Create account' }));

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
    await fireEvent.press(screen.getByRole('button', { name: 'Create account' }));

    expect(await screen.findByText(EMAIL_TAKEN_COPY)).toBeOnTheScreen();
  });

  it('shows the server field errors under the fields', async () => {
    jest
      .spyOn(globalThis, 'fetch')
      .mockImplementation(async () => problemResponse(400, { errors: { password: ['Password must be at least 8 characters.'] } }));
    await render();

    await fill();
    await fireEvent.press(screen.getByRole('button', { name: 'Create account' }));

    expect(await screen.findByText('Password must be at least 8 characters.')).toBeOnTheScreen();
  });

  it('blocks submit when the confirmation does not match', async () => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse(201, makeTokens('Customer')));
    await render();

    await fill();
    await fireEvent.changeText(screen.getByLabelText('Confirm password'), 'something else');
    await fireEvent.press(screen.getByRole('button', { name: 'Create account' }));

    expect(await screen.findByText(PASSWORD_MISMATCH_COPY)).toBeOnTheScreen();
    expect(spy).not.toHaveBeenCalled();
  });

  it('Show reveals both password fields and Hide hides them again', async () => {
    await render();

    expect(screen.getByLabelText('Password').props.secureTextEntry).toBe(true);
    await fireEvent.press(screen.getByRole('button', { name: 'Show password' }));
    expect(screen.getByLabelText('Password').props.secureTextEntry).toBe(false);
    expect(screen.getByLabelText('Confirm password').props.secureTextEntry).toBe(false);
    await fireEvent.press(screen.getByRole('button', { name: 'Hide password' }));
    expect(screen.getByLabelText('Password').props.secureTextEntry).toBe(true);
  });

  it('ticks the length rule as the password grows', async () => {
    await render();

    expect(screen.getByText(/At least 8 characters/)).toHaveTextContent(/\(not met\)/);
    await fireEvent.changeText(screen.getByLabelText('Password'), 'longenough');
    expect(screen.getByText(/At least 8 characters/)).toHaveTextContent(/\(met\)/);
  });
});

describe('device cart notice', () => {
  afterEach(() => useGuestCartStore.setState({ lines: [], mergeKey: null }));

  it('says the device cart will be added to the account only while the device holds lines', async () => {
    await render();
    expect(screen.queryByText(/will be added to your account/)).toBeNull();
  });

  it('shows that line when the device holds cart lines', async () => {
    useGuestCartStore.getState().add(
      {
        variantId: 'v',
        productSlug: 's',
        snapshot: { productName: 'P', variantName: 'V', price: { amount: '1.00', currency: 'USD' }, image: null },
      },
      1,
    );
    await render();
    expect(screen.getByText("Items in this device's cart will be added to your account.")).toBeOnTheScreen();
  });
});
