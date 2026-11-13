import { Text, useWindowDimensions, View } from 'react-native';

import { dashboardLayout } from '../layout';
import { storyStats, USER_STORIES, type UserStory } from '../stories';

const priorityClass = {
  High: 'border-danger-border bg-danger-subtle text-danger',
  Medium: 'border-warning-border bg-warning-subtle text-warning',
} as const;

function Stat({ value, label }: { value: number; label: string }) {
  return (
    <View accessibilityLabel={label} className="rounded-card border border-border bg-background px-lg py-md">
      <Text className="text-kpi font-bold text-foreground">{String(value)}</Text>
      <Text className="text-label font-semibold uppercase text-subtle">{label}</Text>
    </View>
  );
}

function StoryCard({ story }: { story: UserStory }) {
  return (
    <View
      accessibilityLabel={`${story.id} ${story.title}`}
      className="gap-sm rounded-panel border border-border bg-surface p-card web:shadow-card"
    >
      <View className="flex-row flex-wrap items-center justify-between gap-sm">
        <Text className="text-label font-semibold uppercase text-subtle">{story.id}</Text>
        <Text className={`rounded-pill border px-[8px] py-[3px] text-badge font-bold ${priorityClass[story.priority]}`}>
          {story.priority}
        </Text>
      </View>
      <Text accessibilityRole="header" className="text-panel-title font-bold text-foreground">
        {story.title}
      </Text>
      <Text className="text-ui text-muted">{story.quote}</Text>
      <Text className="mt-sm text-meta text-subtle">{`${story.rows.join(' · ')} · ${story.criteria.length} criteria`}</Text>
    </View>
  );
}

/** Static (P2.19): the eight stories, in one column at phone width. */
export function UserStoriesTab() {
  const narrow = dashboardLayout(useWindowDimensions().width).narrow;
  const stats = storyStats(USER_STORIES);
  return (
    <View className="gap-gutter">
      <View className="gap-md rounded-panel border border-border bg-surface p-card">
        <Text accessibilityRole="header" className="text-panel-title font-bold text-foreground">
          User stories
        </Text>
        <Text className="text-ui text-muted">What the admin dashboard is built to do, in the words of the people who use it.</Text>
        <View className="flex-row flex-wrap gap-md">
          <Stat value={stats.stories} label="Stories" />
          <Stat value={stats.criteria} label="Criteria" />
        </View>
      </View>
      <View className="flex-row flex-wrap gap-gutter">
        {USER_STORIES.map((story) => (
          <View key={story.id} className={`min-w-0 grow ${narrow ? 'basis-full' : 'basis-[45%]'}`}>
            <StoryCard story={story} />
          </View>
        ))}
      </View>
    </View>
  );
}
