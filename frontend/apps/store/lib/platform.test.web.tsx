// Runs in the web Jest project only (jest-expo picks *.test.web.* by platform). It renders into the jsdom DOM with
// react-dom: React Native Testing Library drives the native renderer and cannot render react-native-web output.
import * as ImagePicker from 'expo-image-picker';
import * as SecureStore from 'expo-secure-store';
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';

import { PaymentForm, pickImageFile, refreshTokenStorage, refreshTokenTransport, saveTextFile } from './platform';

jest.mock('expo-secure-store');
jest.mock('expo-image-picker', () => ({ launchImageLibraryAsync: jest.fn() }));

const mockConfirmPayment = jest.fn();
const mockLoadStripe = jest.fn();
let mockLoadStripeCalls = 0;
let mockElementsOptions: unknown;

jest.mock('@stripe/stripe-js', () => ({
  loadStripe: (...args: unknown[]) => {
    mockLoadStripeCalls += 1;
    return mockLoadStripe(...args);
  },
}));
jest.mock('@stripe/react-stripe-js', () => ({
  Elements: ({ children, options }: { children: React.ReactNode; options: unknown }) => {
    mockElementsOptions = options;
    return children;
  },
  PaymentElement: () => null,
  useStripe: () => ({ confirmPayment: mockConfirmPayment }),
  useElements: () => ({}),
}));

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const store = SecureStore as jest.Mocked<typeof SecureStore>;
let container: HTMLElement;
let root: Root;

beforeEach(() => {
  mockLoadStripe.mockResolvedValue({});
  process.env.EXPO_PUBLIC_STRIPE_PUBLISHABLE_KEY = 'pk-test-placeholder';
  container = document.createElement('div');
  document.body.appendChild(container);
  root = createRoot(container);
});

afterEach(async () => {
  await act(async () => root.unmount());
  container.remove();
  delete process.env.EXPO_PUBLIC_STRIPE_PUBLISHABLE_KEY;
  jest.clearAllMocks();
});

async function show(onResult = jest.fn(), clientSecret = 'cs_test_1') {
  await act(async () => {
    root.render(<PaymentForm clientSecret={clientSecret} returnPath="/checkout?orderId=o-1" onResult={onResult} />);
  });
  return onResult;
}

const payButton = () => container.querySelector<HTMLElement>('[aria-label="Pay now"]');

async function pay(onResult = jest.fn()) {
  await show(onResult);
  await act(async () => payButton()!.click());
  return onResult;
}

describe('platform (web)', () => {
  // Carried over from the former lib/platform.test.ts "web" case, now without faking Platform.OS.
  it('web uses the cookie transport and never touches SecureStore', async () => {
    expect(refreshTokenTransport()).toBe('cookie');
    await refreshTokenStorage.set('t');
    expect(await refreshTokenStorage.get()).toBeNull();
    await refreshTokenStorage.clear();

    expect(store.setItemAsync).not.toHaveBeenCalled();
    expect(store.getItemAsync).not.toHaveBeenCalled();
    expect(store.deleteItemAsync).not.toHaveBeenCalled();
  });
});

describe('PaymentForm (web payment element)', () => {
  it('passes the client secret to the payment element and confirms without a redirect when none is needed', async () => {
    mockConfirmPayment.mockResolvedValue({ paymentIntent: { status: 'succeeded' } });

    const onResult = await pay();

    expect(onResult).toHaveBeenCalledWith({ status: 'succeeded' });
    expect(mockElementsOptions).toEqual({ clientSecret: 'cs_test_1' });
    expect(mockConfirmPayment).toHaveBeenCalledWith(
      expect.objectContaining({
        redirect: 'if_required',
        confirmParams: { return_url: `${window.location.origin}/checkout?orderId=o-1` },
      }),
    );
  });

  it('loads the payment library once per page, whatever the number of renders', async () => {
    await show(jest.fn(), 'cs_1');
    await show(jest.fn(), 'cs_2');

    expect(mockLoadStripeCalls).toBe(1);
  });

  it('reports processing when the payment is still being processed', async () => {
    mockConfirmPayment.mockResolvedValue({ paymentIntent: { status: 'processing' } });

    expect(await pay()).toHaveBeenCalledWith({ status: 'processing' });
  });

  it('reports a declined card as failed with the provider message', async () => {
    mockConfirmPayment.mockResolvedValue({ error: { message: 'Your card was declined.' } });

    expect(await pay()).toHaveBeenCalledWith({ status: 'failed', message: 'Your card was declined.' });
  });

  it('keeps the form and reports nothing when the card fields are incomplete (validation_error, no payment attempt was made)', async () => {
    mockConfirmPayment.mockResolvedValue({ error: { type: 'validation_error', message: 'Your card number is incomplete.' } });

    expect(await pay()).not.toHaveBeenCalled();
  });

  it('reports an intent that wants another payment method as failed', async () => {
    mockConfirmPayment.mockResolvedValue({ paymentIntent: { status: 'requires_payment_method' } });

    expect(await pay()).toHaveBeenCalledWith({ status: 'failed' });
  });

  it('shows a configuration error and no pay button when the publishable key is missing', async () => {
    delete process.env.EXPO_PUBLIC_STRIPE_PUBLISHABLE_KEY;

    await show();

    expect(container.textContent).toContain('Card payments are not configured in this build.');
    expect(payButton()).toBeNull();
  });
});

describe('saveTextFile (web)', () => {
  it('clicks a download anchor for an object URL of the text and revokes the URL', async () => {
    const createObjectURL = jest.fn(() => 'blob:csv-1');
    const revokeObjectURL = jest.fn();
    Object.assign(URL, { createObjectURL, revokeObjectURL });
    const clicked: { href: string; download: string }[] = [];
    const click = jest.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      clicked.push({ href: this.href, download: this.download });
    });

    await saveTextFile('dashboard-7d.csv', 'text/csv', 'a,b\r\n');

    expect(clicked).toEqual([{ href: 'blob:csv-1', download: 'dashboard-7d.csv' }]);
    expect((createObjectURL.mock.calls[0] as unknown[])[0]).toBeInstanceOf(Blob);
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:csv-1');
    click.mockRestore();
  });
});

describe('pickImageFile (web)', () => {
  const picker = ImagePicker.launchImageLibraryAsync as jest.Mock;

  it('returns a form whose file field holds the browser File, and asks for images only', async () => {
    const file = new File(['x'], 'a.png', { type: 'image/png' });
    picker.mockResolvedValue({ canceled: false, assets: [{ uri: 'blob:x', file }] });

    const form = await pickImageFile();

    expect((form?.get('file') as File).name).toBe('a.png');
    expect(picker).toHaveBeenCalledWith(expect.objectContaining({ mediaTypes: ['images'], allowsEditing: false }));
  });

  it('returns null when the pick is cancelled', async () => {
    picker.mockResolvedValue({ canceled: true, assets: null });

    expect(await pickImageFile()).toBeNull();
  });
});
