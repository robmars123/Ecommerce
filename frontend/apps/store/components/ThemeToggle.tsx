import { Pressable, Text } from 'react-native';

import { useThemeStore } from '@/lib/theme';

export function ThemeToggle() {
  const mode = useThemeStore((state) => state.mode);
  const toggle = useThemeStore((state) => state.toggle);
  return (
    <Pressable
      accessibilityRole="switch"
      accessibilityLabel="Dark mode"
      accessibilityState={{ checked: mode === 'dark' }}
      onPress={toggle}
      className="min-h-touch min-w-touch items-center justify-center rounded-control border border-border px-md"
    >
      <Text className="text-body text-foreground">{mode === 'dark' ? 'Dark' : 'Light'}</Text>
    </Pressable>
  );
}
