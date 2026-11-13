import { Pressable, Text, useWindowDimensions, View } from 'react-native';

import { DASHBOARD_TABS, type DashboardTab } from '../tabs';
import { dashboardLayout } from '../layout';

/** The Overview / User Stories / Acceptance Criteria switch (css §4 view tabs; smaller type at phone width, §10). */
export function ViewTabs({ tab, onChange }: { tab: DashboardTab; onChange: (next: DashboardTab) => void }) {
  const narrow = dashboardLayout(useWindowDimensions().width).narrow;
  return (
    <View accessibilityRole="tablist" className="flex-row flex-wrap gap-xs border-b border-border">
      {DASHBOARD_TABS.map((item) => {
        const selected = item.tab === tab;
        return (
          <Pressable
            key={item.tab}
            accessibilityRole="tab"
            accessibilityState={{ selected }}
            onPress={() => onChange(item.tab)}
            className={`min-h-touch justify-center border-b-2 ${narrow ? 'px-md' : 'px-lg'} ${selected ? 'border-primary' : 'border-transparent'}`}
          >
            <Text className={`${narrow ? 'text-meta' : 'text-ui'} font-semibold ${selected ? 'text-foreground' : 'text-muted'}`}>
              {item.label}
            </Text>
          </Pressable>
        );
      })}
    </View>
  );
}
