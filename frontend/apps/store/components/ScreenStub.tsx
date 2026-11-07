import { Stack } from 'expo-router';
import { Text, View } from 'react-native';
import type { ReactNode } from 'react';

type ScreenStubProps = {
  title: string;
  detail?: string;
  children?: ReactNode;
};

export function ScreenStub({ title, detail, children }: ScreenStubProps) {
  return (
    <View className="flex-1 gap-sm p-lg">
      <Stack.Screen options={{ title }} />
      <Text accessibilityRole="header" className="text-heading font-semibold text-foreground">
        {title}
      </Text>
      {detail ? <Text className="text-body text-foreground">{detail}</Text> : null}
      <Text className="text-body text-muted">Not built yet.</Text>
      {children}
    </View>
  );
}
