import { Text, View } from 'react-native';

import { DEFINITION_OF_DONE, USER_STORIES } from '../stories';

/** Static (P2.20): each story's criteria with a check bullet, then the Definition of Done bar. */
export function AcceptanceCriteriaTab() {
  return (
    <View className="gap-gutter">
      {USER_STORIES.map((story) => (
        <View key={story.id} className="gap-sm rounded-panel border border-border bg-surface p-card web:shadow-card">
          <Text accessibilityRole="header" className="text-panel-title font-bold text-foreground">{`${story.id} · ${story.title}`}</Text>
          {story.criteria.map((criterion) => (
            <View key={criterion} className="flex-row items-start gap-sm">
              <Text accessibilityElementsHidden importantForAccessibility="no-hide-descendants" className="text-ui text-primary-text">
                ✓
              </Text>
              <Text className="min-w-0 shrink text-ui text-muted">{criterion}</Text>
            </View>
          ))}
        </View>
      ))}
      <View className="gap-sm rounded-panel border border-primary bg-primary-subtle p-card">
        <Text accessibilityRole="header" className="text-panel-title font-bold text-primary-text">
          Definition of Done
        </Text>
        {DEFINITION_OF_DONE.map((rule) => (
          <Text key={rule} className="text-ui text-foreground">
            {rule}
          </Text>
        ))}
      </View>
    </View>
  );
}
