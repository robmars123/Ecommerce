import { Stack, useLocalSearchParams, useRouter } from 'expo-router';
import { ScrollView, Text, View } from 'react-native';

import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { PeriodSwitch } from '@/features/admin-dashboard/components/PeriodSwitch';
import { RevenueTrendPanel } from '@/features/admin-dashboard/components/RevenueTrendPanel';
import { TopProductsPanel } from '@/features/admin-dashboard/components/TopProductsPanel';
import { useDashboardQuery } from '@/features/admin-dashboard/hooks/hooks';
import { parseDashboardPeriod } from '@/features/admin-dashboard/period';
import type { DashboardPeriod } from '@/features/admin-dashboard/types';
import { adminRouteDecision } from '@/features/auth/admin-access';
import { useSession } from '@/features/auth/hooks/useSession';
import { userMessage } from '@/lib/api-client';

/** Revenue trend and top products for one period (P2-tier4 D15). Admin only; top products reuse the dashboard query. */
export function AnalyticsView() {
  const params = useLocalSearchParams<{ period?: string | string[] }>();
  const router = useRouter();
  const period = parseDashboardPeriod(params.period);
  const choosePeriod = (next: DashboardPeriod) => router.setParams({ period: next === 'today' ? undefined : next });
  const role = useSession().user?.role ?? null;
  const isAdmin = adminRouteDecision(role, '/admin/analytics') === null;
  const dashboard = useDashboardQuery(period);

  if (!isAdmin) {
    return null;
  }

  return (
    <ScrollView contentContainerClassName="w-full max-w-content gap-gutter self-center p-lg">
      <Stack.Screen options={{ title: 'Analytics' }} />
      <View className="flex-row flex-wrap items-center justify-between gap-lg">
        <Text accessibilityRole="header" className="text-heading font-semibold text-foreground">
          Analytics
        </Text>
        <PeriodSwitch period={period} onChange={choosePeriod} />
      </View>
      <RevenueTrendPanel period={period} />
      {dashboard.data ? (
        <TopProductsPanel products={dashboard.data.topProducts} />
      ) : dashboard.isError ? (
        <ErrorState message={userMessage(dashboard.error)} onRetry={() => void dashboard.refetch()} />
      ) : (
        <LoadingState />
      )}
    </ScrollView>
  );
}
