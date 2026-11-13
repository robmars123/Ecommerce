import { Text, View } from 'react-native';

import { formatOrderDate, statusLabel } from '../status-labels';
import type { TimelineEntry } from '../types';

/** Shown in the order the server sends: oldest first. A rail of dots joined by a line; each step shows its server-written description; a cancelled step is red with a hidden "×". */
export function OrderTimeline({ entries }: { entries: TimelineEntry[] }) {
  return (
    <View accessibilityRole="list">
      {entries.map((entry, index) => {
        const cancelled = entry.toStatus === 'Cancelled';
        const last = index === entries.length - 1;
        return (
          <View key={`${entry.at}-${index}`} className="flex-row gap-md">
            <View className="w-[18px] items-center">
              <View
                className={`mt-[3px] h-[18px] w-[18px] items-center justify-center rounded-pill border-2 ${cancelled ? 'border-danger bg-danger' : 'border-muted bg-muted'}`}
              >
                {cancelled ? (
                  <Text
                    accessibilityElementsHidden
                    importantForAccessibility="no-hide-descendants"
                    className="text-badge font-bold text-danger-foreground"
                  >
                    ×
                  </Text>
                ) : null}
              </View>
              {last ? null : <View className="w-[2px] flex-1 bg-border" />}
            </View>
            <View className={`min-w-0 flex-1 gap-xs ${last ? '' : 'pb-lg'}`}>
              <View className="flex-row flex-wrap items-baseline gap-sm">
                <Text className="text-ui font-bold text-foreground">{statusLabel(entry.toStatus)}</Text>
                <Text className="text-ui-sm text-muted">{formatOrderDate(entry.at)}</Text>
              </View>
              <Text className="text-ui-sm text-muted">{entry.description}</Text>
              {entry.reason ? (
                <Text className="self-start rounded-[6px] border border-danger-border bg-danger-subtle px-sm py-[2px] font-mono text-label font-semibold text-danger">
                  {entry.reason}
                </Text>
              ) : null}
            </View>
          </View>
        );
      })}
    </View>
  );
}
