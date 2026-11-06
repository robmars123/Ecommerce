import { fireEvent, screen } from 'expo-router/testing-library';

import { renderRouterWithQuery } from '@/tests/render-with-query';

import { PasswordResetRequestForm, RESET_REQUESTED_COPY } from './PasswordResetRequestForm';

beforeEach(() => {
  process.env.EXPO_PUBLIC_API_BASE_URL = 'http://api.test.invalid:5080';
});

afterEach(() => {
  jest.restoreAllMocks();
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

describe('PasswordResetRequestForm', () => {
  it.each(['known@example.test', 'nobody@example.test'])('shows the same message for %s', async (email) => {
    const spy = jest.spyOn(globalThis, 'fetch').mockImplementation(async () => new Response('', { status: 202 }));
    await renderRouterWithQuery({ index: PasswordResetRequestForm }, { initialUrl: '/' });

    await fireEvent.changeText(screen.getByLabelText('Email'), email);
    await fireEvent.press(screen.getByRole('button', { name: 'Send reset link' }));

    expect(await screen.findByText(RESET_REQUESTED_COPY)).toBeOnTheScreen();
    expect(JSON.parse((spy.mock.calls[0][1] as RequestInit).body as string)).toEqual({ email });
  });
});
