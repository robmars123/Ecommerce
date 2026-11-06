import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

import { refreshTokenStorage, refreshTokenTransport } from './platform';

jest.mock('expo-secure-store');

const store = SecureStore as jest.Mocked<typeof SecureStore>;
const platform = Platform as { OS: string };
const ORIGINAL_OS = Platform.OS;

afterEach(() => {
  platform.OS = ORIGINAL_OS;
  jest.clearAllMocks();
});

describe('platform', () => {
  it('web uses the cookie transport and never touches SecureStore', async () => {
    platform.OS = 'web';

    expect(refreshTokenTransport()).toBe('cookie');
    await refreshTokenStorage.set('t');
    expect(await refreshTokenStorage.get()).toBeNull();
    await refreshTokenStorage.clear();

    expect(store.setItemAsync).not.toHaveBeenCalled();
    expect(store.getItemAsync).not.toHaveBeenCalled();
    expect(store.deleteItemAsync).not.toHaveBeenCalled();
  });

  it('native uses the body transport and the fixed key with a device-only keychain option', async () => {
    platform.OS = 'android';
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
