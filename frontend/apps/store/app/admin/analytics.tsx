import { Stack } from 'expo-router';

import { AnalyticsView } from '@/features/admin-analytics/components/AnalyticsView';

export default function AdminAnalyticsScreen() {
  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <AnalyticsView />
    </>
  );
}
