import type { ReactNode } from 'react';
import { Text, View } from 'react-native';

/** A bordered surface for one block of an admin screen, with an optional heading. */
export function AdminPanel({ title, children }: { title?: string; children: ReactNode }) {
  return (
    <View className="gap-md rounded-card border border-border bg-surface p-lg">
      {title ? (
        <Text accessibilityRole="header" className="text-title font-semibold text-foreground">
          {title}
        </Text>
      ) : null}
      {children}
    </View>
  );
}
