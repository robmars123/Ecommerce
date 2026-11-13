// The only per-platform code (architecture 5.2): native half. Keep the exports identical to platform.d.ts.
import { StripeProvider, useStripe } from '@stripe/stripe-react-native';
import Constants from 'expo-constants';
import * as Linking from 'expo-linking';
import * as SecureStore from 'expo-secure-store';
import { useState } from 'react';
import { Share, Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';

import { getStripePublishableKey } from './config';
import type { PaymentFormProps } from './platform';

const REFRESH_TOKEN_KEY = 'ecommerce.refreshToken';
const SHEET_CANCELED = 'Canceled';

/** Opens the share sheet with the text (React Native core; no file-system package). */
export async function saveTextFile(fileName: string, _mimeType: string, text: string): Promise<void> {
  await Share.share({ title: fileName, message: text });
}

export function refreshTokenTransport(): 'cookie' | 'body' {
  return 'body';
}

export const refreshTokenStorage = {
  async get(): Promise<string | null> {
    return SecureStore.getItemAsync(REFRESH_TOKEN_KEY);
  },
  async set(token: string): Promise<void> {
    await SecureStore.setItemAsync(REFRESH_TOKEN_KEY, token, {
      keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
    });
  },
  async clear(): Promise<void> {
    await SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY);
  },
};

function PaymentSheetButton({ clientSecret, returnPath, onResult }: PaymentFormProps) {
  const { initPaymentSheet, presentPaymentSheet } = useStripe();
  const [busy, setBusy] = useState(false);

  async function pay() {
    setBusy(true);
    try {
      const init = await initPaymentSheet({
        paymentIntentClientSecret: clientSecret,
        merchantDisplayName: Constants.expoConfig?.name ?? 'Store',
        returnURL: Linking.createURL(returnPath),
      });
      if (init.error) {
        onResult({ status: 'failed', message: init.error.message });
        return;
      }
      const { error } = await presentPaymentSheet();
      if (!error) {
        onResult({ status: 'succeeded' });
      } else if (error.code === SHEET_CANCELED) {
        onResult({ status: 'canceled' });
      } else {
        onResult({ status: 'failed', message: error.message });
      }
    } finally {
      setBusy(false);
    }
  }

  return <ActionButton label="Pay now" onPress={() => void pay()} pending={busy} />;
}

/** The provider payment sheet. The publishable key is public by design; a missing one is a visible configuration error. */
export function PaymentForm(props: PaymentFormProps) {
  const publishableKey = getStripePublishableKey();
  if (!publishableKey) {
    return (
      <Text accessibilityRole="alert" className="text-body text-danger">
        Card payments are not configured in this build.
      </Text>
    );
  }
  return (
    <StripeProvider publishableKey={publishableKey}>
      <View className="gap-md">
        <PaymentSheetButton {...props} />
      </View>
    </StripeProvider>
  );
}
