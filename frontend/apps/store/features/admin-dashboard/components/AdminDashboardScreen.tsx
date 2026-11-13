import { Stack, useLocalSearchParams, useRouter } from 'expo-router';
import { ScrollView, Text, useWindowDimensions, View } from 'react-native';

import { dashboardLayout } from '../layout';
import { parseDashboardTab, type DashboardTab } from '../tabs';
import { AcceptanceCriteriaTab } from './AcceptanceCriteriaTab';
import { DashboardView } from './DashboardView';
import { UserStoriesTab } from './UserStoriesTab';
import { ViewTabs } from './ViewTabs';

/** The tab lives in ?tab= (missing or unknown is Overview); setParams keeps ?period=. Static tabs mount no dashboard queries. */
export function AdminDashboardScreen() {
  const params = useLocalSearchParams<{ tab?: string | string[] }>();
  const router = useRouter();
  const tab = parseDashboardTab(params.tab);
  const layout = dashboardLayout(useWindowDimensions().width);
  const tabs = <ViewTabs tab={tab} onChange={(next: DashboardTab) => router.setParams({ tab: next === 'overview' ? undefined : next })} />;

  if (tab === 'overview') {
    return <DashboardView tabs={tabs} />;
  }
  return (
    <ScrollView contentContainerClassName={`w-full gap-gutter ${layout.wide ? 'px-page pb-[36px]' : 'px-lg pb-[28px]'}`}>
      <Stack.Screen options={{ title: 'Admin dashboard' }} />
      <View className="pb-lg pt-[22px]">
        <Text
          accessibilityRole="header"
          className={`${layout.narrow ? 'text-page-title-sm' : 'text-page-title'} font-bold text-foreground`}
        >
          Admin dashboard
        </Text>
      </View>
      {tabs}
      {tab === 'stories' ? <UserStoriesTab /> : <AcceptanceCriteriaTab />}
    </ScrollView>
  );
}
