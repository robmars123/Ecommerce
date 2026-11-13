import { ScrollView, Text, View } from 'react-native';

import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { Panel } from '@/components/Panel';
import { ScreenTitle } from '@/components/ScreenTitle';
import { adminRouteDecision } from '@/features/auth/admin-access';
import { useSession } from '@/features/auth/hooks/useSession';
import { userMessage } from '@/lib/api-client';

import { useAdminSettingsQuery } from '../hooks';

function Row({ label, value }: { label: string; value: string }) {
  return (
    <View className="flex-row flex-wrap items-center justify-between gap-md border-b border-border py-md">
      <Text className="text-ui text-muted">{label}</Text>
      <Text className="text-ui font-semibold text-foreground">{value}</Text>
    </View>
  );
}

/** The two values the store runs on, read-only (P2-tier4 D7). Changing them is a server configuration change. */
export function AdminSettingsView() {
  const role = useSession().user?.role ?? null;
  const query = useAdminSettingsQuery(adminRouteDecision(role, '/admin/settings') === null);
  const data = query.data;

  return (
    <ScrollView contentContainerClassName="w-full max-w-content gap-lg self-center p-lg">
      <ScreenTitle title="Settings" />
      <Panel>
        {data ? (
          <View>
            <Row label="Store currency" value={data.currency} />
            <Row label="Low-stock threshold" value={`${data.lowStockThreshold} units available or fewer`} />
            <Text className="pt-md text-meta text-subtle">Read-only. Set in server configuration.</Text>
          </View>
        ) : query.isError ? (
          <ErrorState message={userMessage(query.error)} onRetry={() => void query.refetch()} />
        ) : (
          <LoadingState />
        )}
      </Panel>
    </ScrollView>
  );
}
