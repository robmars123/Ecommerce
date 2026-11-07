// Runs in the ios and android Jest projects only (jest-expo picks *.test.native.* by platform).
import { useStripe } from '@stripe/stripe-react-native';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import * as SecureStore from 'expo-secure-store';

import { PaymentForm, refreshTokenStorage, refreshTokenTransport } from './platform';

jest.mock('expo-secure-store');
jest.mock('expo-constants', () => ({ expoConfig: { name: 'Ecommerce Store' } }));
jest.mock('expo-linking', () => ({ createURL: (path: string) => `ecommerce-store://${path.replace(/^\//, '')}` }));

const store = SecureStore as jest.Mocked<typeof SecureStore>;
const initPaymentSheet = jest.fn();
const presentPaymentSheet = jest.fn();

jest.mock('@stripe/stripe-react-native', () => ({
  StripeProvider: ({ children }: { children: React.ReactNode }) => children,
  useStripe: jest.fn(),
}));

beforeEach(() => {
  (useStripe as jest.Mock).mockReturnValue({ initPaymentSheet, presentPaymentSheet });
  process.env.EXPO_PUBLIC_STRIPE_PUBLISHABLE_KEY = 'pk-test-placeholder';
});

afterEach(() => {
  delete process.env.EXPO_PUBLIC_STRIPE_PUBLISHABLE_KEY;
  jest.clearAllMocks();
});

describe('platform (native)', () => {
  // Carried over from the former lib/platform.test.ts "native" case, now without faking Platform.OS.
  it('native uses the body transport and the fixed key with a device-only keychain option', async () => {
    store.getItemAsync.mockResolvedValue('stored');

    expect(refreshTokenTransport()).toBe('body');
    await refreshTokenStorage.set('t');
    expect(await refreshTokenStorage.get()).toBe('stored');
    await refreshTokenStorage.clear();

    expect(store.setItemAsync).toHaveBeenCalledWith('ecommerce.refreshToken', 't', {
      keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
    });
    expect(store.getItemAsync).toHaveBeenCalledWith('ecommerce.refreshToken');
    expect(store.deleteItemAsync).toHaveBeenCalledWith('ecommerce.refreshToken');
  });
});

describe('PaymentForm (native payment sheet)', () => {
  async function pay(onResult = jest.fn()) {
    await render(<PaymentForm clientSecret="cs_test_1" returnPath="/checkout?orderId=o-1" onResult={onResult} />);
    await fireEvent.press(screen.getByRole('button', { name: 'Pay now' }));
    return onResult;
  }

  it('hands the client secret, the store name and a return URL to the payment sheet', async () => {
    initPaymentSheet.mockResolvedValue({});
    presentPaymentSheet.mockResolvedValue({});

    const onResult = await pay();

    await waitFor(() => expect(onResult).toHaveBeenCalledWith({ status: 'succeeded' }));
    expect(initPaymentSheet).toHaveBeenCalledWith({
      paymentIntentClientSecret: 'cs_test_1',
      merchantDisplayName: 'Ecommerce Store',
      returnURL: 'ecommerce-store://checkout?orderId=o-1',
    });
    expect(presentPaymentSheet).toHaveBeenCalledTimes(1);
  });

  it('reports a dismissed sheet as canceled', async () => {
    initPaymentSheet.mockResolvedValue({});
    presentPaymentSheet.mockResolvedValue({ error: { code: 'Canceled', message: 'The payment flow has been canceled' } });

    const onResult = await pay();

    await waitFor(() => expect(onResult).toHaveBeenCalledWith({ status: 'canceled' }));
  });

  it('reports a declined card as failed with the provider message', async () => {
    initPaymentSheet.mockResolvedValue({});
    presentPaymentSheet.mockResolvedValue({ error: { code: 'Failed', message: 'Your card was declined.' } });

    const onResult = await pay();

    await waitFor(() => expect(onResult).toHaveBeenCalledWith({ status: 'failed', message: 'Your card was declined.' }));
  });

  it('reports an initialisation error as failed and never presents the sheet', async () => {
    initPaymentSheet.mockResolvedValue({ error: { code: 'Failed', message: 'Could not start.' } });

    const onResult = await pay();

    await waitFor(() => expect(onResult).toHaveBeenCalledWith({ status: 'failed', message: 'Could not start.' }));
    expect(presentPaymentSheet).not.toHaveBeenCalled();
  });

  it('shows a configuration error and no pay button when the publishable key is missing', async () => {
    delete process.env.EXPO_PUBLIC_STRIPE_PUBLISHABLE_KEY;

    await render(<PaymentForm clientSecret="cs_test_1" returnPath="/checkout" onResult={jest.fn()} />);

    expect(screen.getByRole('alert')).toHaveTextContent('Card payments are not configured in this build.');
    expect(screen.queryByRole('button', { name: 'Pay now' })).not.toBeOnTheScreen();
  });
});
