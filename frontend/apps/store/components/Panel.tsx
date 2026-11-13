import type { ReactNode } from 'react';
import { Text, View } from 'react-native';

/** A bordered surface for one block of a screen, with an optional heading and a muted sub line beside it. */
export function Panel({ title, subtitle, children }: { title?: string; subtitle?: string; children: ReactNode }) {
  return (
    <View className="overflow-hidden rounded-panel border border-border bg-surface web:shadow-card">
      {title ? (
        <View className="flex-row items-center justify-between gap-md border-b border-border px-card pb-md pt-lg">
          <Text accessibilityRole="header" className="text-panel-title font-bold text-foreground">
            {title}
          </Text>
          {subtitle ? <Text className="text-ui-sm text-muted">{subtitle}</Text> : null}
        </View>
      ) : null}
      <View className="gap-md px-card pb-card pt-lg">{children}</View>
    </View>
  );
}
