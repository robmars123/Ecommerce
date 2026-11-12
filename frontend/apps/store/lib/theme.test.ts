import AsyncStorage from '@react-native-async-storage/async-storage';
import { act, renderHook } from '@testing-library/react-native';

import { themes } from '@/lib/design-tokens';

import { createThemeStore, themeVariables, useThemeColors, useThemeStore } from './theme';

const flush = () => new Promise((resolve) => setTimeout(resolve, 0));

describe('theme store', () => {
  beforeEach(async () => {
    await AsyncStorage.clear();
    useThemeStore.setState({ mode: 'dark' });
  });

  it('TH1: empty storage means dark', async () => {
    const store = createThemeStore();
    await store.persist.rehydrate();
    expect(store.getState().mode).toBe('dark');
  });

  it('TH2: toggle goes dark, light, dark', () => {
    const store = createThemeStore();
    store.getState().toggle();
    expect(store.getState().mode).toBe('light');
    store.getState().toggle();
    expect(store.getState().mode).toBe('dark');
  });

  it('TH3: the mode survives a restart', async () => {
    const first = createThemeStore();
    first.getState().toggle();
    await flush();

    const restarted = createThemeStore();
    await restarted.persist.rehydrate();
    expect(restarted.getState().mode).toBe('light');
  });

  it.each([['{"state":{"mode":"blue"},"version":1}'], ['not json {{']])('TH4: bad stored data %s falls back to dark', async (stored) => {
    await AsyncStorage.setItem('theme', stored);
    const store = createThemeStore();
    await store.persist.rehydrate();
    expect(store.getState().mode).toBe('dark');
  });

  it.each(['dark', 'light'] as const)('TH5: themeVariables(%s) has one --color-<key> per token', (mode) => {
    const expected = Object.fromEntries(Object.entries(themes[mode]).map(([key, value]) => [`--color-${key}`, value]));
    expect(themeVariables(mode)).toEqual(expected);
  });

  it('TH6: useThemeColors follows the store', async () => {
    const { result } = await renderHook(() => useThemeColors());
    expect(result.current).toBe(themes.dark);
    await act(async () => useThemeStore.getState().toggle());
    expect(result.current).toBe(themes.light);
  });
});
