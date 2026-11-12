import AsyncStorage from '@react-native-async-storage/async-storage';
import { vars } from 'nativewind';
import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';

import { themes } from '@/lib/design-tokens';

export type ThemeMode = 'dark' | 'light';

export type ThemeState = {
  mode: ThemeMode;
  toggle: () => void;
};

function isThemeMode(value: unknown): value is ThemeMode {
  return value === 'dark' || value === 'light';
}

/** A factory so tests can start a fresh store (a restart) over the same mocked storage. */
export function createThemeStore() {
  return create<ThemeState>()(
    persist(
      (set) => ({
        mode: 'dark',
        toggle: () => set((state) => ({ mode: state.mode === 'dark' ? 'light' : 'dark' })),
      }),
      {
        name: 'theme',
        version: 1,
        storage: createJSONStorage(() => AsyncStorage),
        partialize: (state) => ({ mode: state.mode }),
        // A stored mode that is not exactly dark/light is ignored: an unknown key would index themes and crash.
        merge: (persisted, current) => {
          const stored = (persisted as { mode?: unknown } | undefined)?.mode;
          return isThemeMode(stored) ? { ...current, mode: stored } : current;
        },
      },
    ),
  );
}

/** The app's one device theme preference. */
export const useThemeStore = createThemeStore();

/** The CSS variables Tailwind's colour utilities resolve against (names match `colorVariables`). */
export function themeVariables(mode: ThemeMode): Record<string, string> {
  return Object.fromEntries(Object.entries(themes[mode]).map(([key, value]) => [`--color-${key}`, value]));
}

/** For JS props that take a colour value, not a class (arch 5.5). */
export function useThemeColors() {
  return themes[useThemeStore((state) => state.mode)];
}

export function useThemeStyle() {
  return vars(themeVariables(useThemeStore((state) => state.mode)));
}
