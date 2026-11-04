import { Stack } from 'expo-router';
import { StyleSheet, Text, View } from 'react-native';
import type { ReactNode } from 'react';

type ScreenStubProps = {
  title: string;
  detail?: string;
  children?: ReactNode;
};

export function ScreenStub({ title, detail, children }: ScreenStubProps) {
  return (
    <View style={styles.container}>
      <Stack.Screen options={{ title }} />
      <Text accessibilityRole="header" style={styles.title}>
        {title}
      </Text>
      {detail ? <Text style={styles.detail}>{detail}</Text> : null}
      <Text style={styles.note}>Not built yet.</Text>
      {children}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, padding: 16, gap: 8 },
  title: { fontSize: 24, fontWeight: '600' },
  detail: { fontSize: 16 },
  note: { fontSize: 16, opacity: 0.7 },
});
