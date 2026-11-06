// The only per-platform code (architecture 5.2, D19). Split into platform.native.ts / platform.web.ts when the
// payments SDK or push arrive; until then one Platform.OS check keeps it in one place.
import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

const REFRESH_TOKEN_KEY = 'ecommerce.refreshToken';

/** Web keeps the refresh token in an httpOnly cookie the script cannot read; native keeps it in the body and SecureStore. */
export function refreshTokenTransport(): 'cookie' | 'body' {
  return Platform.OS === 'web' ? 'cookie' : 'body';
}

export const refreshTokenStorage = {
  async get(): Promise<string | null> {
    if (Platform.OS === 'web') return null;
    return SecureStore.getItemAsync(REFRESH_TOKEN_KEY);
  },
  async set(token: string): Promise<void> {
    if (Platform.OS === 'web') return;
    await SecureStore.setItemAsync(REFRESH_TOKEN_KEY, token, {
      keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
    });
  },
  async clear(): Promise<void> {
    if (Platform.OS === 'web') return;
    await SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY);
  },
};
