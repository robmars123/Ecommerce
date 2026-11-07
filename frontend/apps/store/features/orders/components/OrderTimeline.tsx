import { Text, View } from 'react-native';

import { formatOrderDate, statusLabel } from '../status-labels';
import type { TimelineEntry } from '../types';

/** Shown in the order the server sends: oldest first. */
export function OrderTimeline({ entries }: { entries: TimelineEntry[] }) {
  return (
    <View accessibilityRole="list" className="gap-sm">
      {entries.map((entry, index) => (
        <View key={`${entry.at}-${index}`} className="flex-row flex-wrap items-baseline gap-sm">
          <Text className="text-body font-semibold text-foreground">{statusLabel(entry.toStatus)}</Text>
          <Text className="text-small text-muted">{formatOrderDate(entry.at)}</Text>
          {entry.reason ? <Text className="text-small text-muted">{entry.reason}</Text> : null}
        </View>
      ))}
    </View>
  );
}
