import { StatusBar } from 'expo-status-bar';
import type { ReactNode } from 'react';
import { View } from 'react-native';

import { useThemeStore, useThemeStyle } from '@/lib/theme';

/** Sets the --color-* variables every Tailwind colour class resolves against (arch 5.5). */
export function ThemeRoot({ children }: { children: ReactNode }) {
  const style = useThemeStyle();
  const mode = useThemeStore((state) => state.mode);
  return (
    <View style={style} className="flex-1 bg-background">
      <StatusBar style={mode === 'dark' ? 'light' : 'dark'} />
      {children}
    </View>
  );
}
