import { Stack } from 'expo-router';
import { StyleSheet, Text } from 'react-native';

/** The screen's header title and its on-page heading. */
export function ScreenTitle({ title }: { title: string }) {
  return (
    <>
      <Stack.Screen options={{ title }} />
      <Text accessibilityRole="header" style={styles.title}>
        {title}
      </Text>
    </>
  );
}

const styles = StyleSheet.create({
  title: { fontSize: 24, fontWeight: '600' },
});
