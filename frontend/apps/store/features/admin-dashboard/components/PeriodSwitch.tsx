import { Pressable, Text, View } from 'react-native';

import { PERIODS } from '../period';
import type { DashboardPeriod } from '../types';

/** The Today / 7 days / 30 days segmented control, shared by the dashboard and the analytics page. */
export function PeriodSwitch({ period, onChange }: { period: DashboardPeriod; onChange: (next: DashboardPeriod) => void }) {
  return (
    <View className="flex-row rounded-pill border border-border bg-surface p-[3px] web:shadow-card">
      {PERIODS.map((p) => (
        <Pressable
          key={p.value}
          accessibilityRole="button"
          accessibilityState={{ selected: p.value === period }}
          onPress={() => onChange(p.value)}
          className={`min-h-touch justify-center rounded-pill px-[14px] ${p.value === period ? 'bg-selected' : ''}`}
        >
          <Text className={`text-ui-sm font-semibold ${p.value === period ? 'text-selected-foreground' : 'text-muted'}`}>{p.label}</Text>
        </Pressable>
      ))}
    </View>
  );
}
