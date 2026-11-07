import { Stack } from 'expo-router';
import { Text } from 'react-native';

/** The screen's header title and its on-page heading. */
export function ScreenTitle({ title }: { title: string }) {
  return (
    <>
      <Stack.Screen options={{ title }} />
      <Text accessibilityRole="header" className="text-heading font-semibold text-foreground">
        {title}
      </Text>
    </>
  );
}
