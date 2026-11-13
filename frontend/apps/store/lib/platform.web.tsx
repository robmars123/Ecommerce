// The only per-platform code (architecture 5.2): web half. Keep the exports identical to platform.d.ts.
import { Elements, PaymentElement, useElements, useStripe } from '@stripe/react-stripe-js';
import { loadStripe, type Stripe } from '@stripe/stripe-js';
import * as ImagePicker from 'expo-image-picker';
import { useState } from 'react';
import { Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';

import { getStripePublishableKey } from './config';
import type { PaymentFormProps } from './platform';

/** Downloads the text as a file: a Blob behind an object URL on a temporary anchor, revoked afterwards. */
export async function saveTextFile(fileName: string, mimeType: string, text: string): Promise<void> {
  const url = URL.createObjectURL(new Blob([text], { type: mimeType }));
  try {
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
  } finally {
    URL.revokeObjectURL(url);
  }
}

/** A file chooser; the picked asset carries the browser File, which goes into the form as is. */
export async function pickImageFile(): Promise<FormData | null> {
  const result = await ImagePicker.launchImageLibraryAsync({ mediaTypes: ['images'], allowsEditing: false, quality: 0.9 });
  const asset = result.canceled ? undefined : result.assets[0];
  if (!asset?.file) {
    return null;
  }
  const form = new FormData();
  form.append('file', asset.file);
  return form;
}

export function refreshTokenTransport(): 'cookie' | 'body' {
  return 'cookie';
}

// The refresh token lives in an httpOnly cookie the script cannot read, so there is nothing to store.
export const refreshTokenStorage = {
  async get(): Promise<string | null> {
    return null;
  },
  async set(): Promise<void> {},
  async clear(): Promise<void> {},
};

// Loaded once per page: loadStripe injects the provider script and must not run twice for one key.
let stripePromise: Promise<Stripe | null> | undefined;
function stripeFor(publishableKey: string): Promise<Stripe | null> {
  stripePromise ??= loadStripe(publishableKey);
  return stripePromise;
}

function PayButton({ returnPath, onResult }: Pick<PaymentFormProps, 'returnPath' | 'onResult'>) {
  const stripe = useStripe();
  const elements = useElements();
  const [busy, setBusy] = useState(false);

  async function pay() {
    if (!stripe || !elements) {
      return;
    }
    setBusy(true);
    try {
      // 'if_required': a card that needs no redirect resolves here; 3-D Secure and bank pages return to returnPath.
      const { error, paymentIntent } = await stripe.confirmPayment({
        elements,
        confirmParams: { return_url: `${window.location.origin}${returnPath}` },
        redirect: 'if_required',
      });
      if (error?.type === 'validation_error') {
        // An incomplete field: nothing was sent to the provider, so this is not a payment attempt. The element shows the message.
        return;
      }
      if (error) {
        onResult({ status: 'failed', message: error.message });
      } else if (paymentIntent?.status === 'succeeded') {
        onResult({ status: 'succeeded' });
      } else if (paymentIntent?.status === 'requires_payment_method') {
        onResult({ status: 'failed' });
      } else {
        onResult({ status: 'processing' });
      }
    } finally {
      setBusy(false);
    }
  }

  return <ActionButton label="Pay now" onPress={() => void pay()} pending={busy} disabled={!stripe || !elements} />;
}

/** The provider payment element. The publishable key is public by design; a missing one is a visible configuration error. */
export function PaymentForm({ clientSecret, returnPath, onResult }: PaymentFormProps) {
  const publishableKey = getStripePublishableKey();
  if (!publishableKey) {
    return (
      <Text accessibilityRole="alert" className="text-body text-danger">
        Card payments are not configured in this build.
      </Text>
    );
  }
  return (
    <Elements stripe={stripeFor(publishableKey)} options={{ clientSecret }}>
      <View className="gap-md">
        <PaymentElement />
        <PayButton returnPath={returnPath} onResult={onResult} />
      </View>
    </Elements>
  );
}
