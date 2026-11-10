import { Link, useLocalSearchParams, useRouter } from 'expo-router';
import { ScrollView, Text, View } from 'react-native';

import { EmptyState } from '@/components/EmptyState';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { ScreenTitle } from '@/components/ScreenTitle';
import { Pagination } from '@/features/catalog/components/Pagination';
import { SearchBar } from '@/features/catalog/components/SearchBar';
import { formatOrderDate } from '@/features/orders/status-labels';
import { userMessage } from '@/lib/api-client';

import { useAdminCustomersQuery } from '../hooks/useAdminCustomersQuery';

type RawParams = { search?: string; page?: string };

function parseParams(raw: RawParams): { search: string | undefined; page: number } {
  const search = raw.search?.trim().slice(0, 100);
  const page = raw.page && /^\d+$/.test(raw.page) && Number(raw.page) >= 1 ? Number(raw.page) : 1;
  return { search: search || undefined, page };
}

export function AdminCustomerListView() {
  const router = useRouter();
  const { search, page } = parseParams(useLocalSearchParams<RawParams>());
  const customers = useAdminCustomersQuery(search, page);

  /** Both keys are always present so router.setParams removes the ones that are no longer set. */
  function change(next: { search?: string; page: number }) {
    router.setParams({ search: next.search || undefined, page: next.page > 1 ? String(next.page) : undefined });
  }

  const data = customers.data;

  return (
    <ScrollView contentContainerClassName="gap-md p-lg">
      <ScreenTitle title="Admin · Customers" />
      <SearchBar value={search ?? ''} onSearch={(text) => change({ search: text || undefined, page: 1 })} />

      {data ? (
        data.items.length === 0 ? (
          <EmptyState message="No customers match." />
        ) : (
          <View className="gap-xs">
            {data.items.map((item) => (
              <Link key={item.id} href={{ pathname: '/admin/customers/[id]', params: { id: item.id } }} accessibilityRole="link" asChild>
                <Text className="min-h-touch py-md text-body text-foreground">
                  {`${item.displayName} · ${item.email} · Joined ${formatOrderDate(item.createdAt)}`}
                </Text>
              </Link>
            ))}
          </View>
        )
      ) : customers.isError ? (
        <ErrorState message={userMessage(customers.error)} onRetry={() => void customers.refetch()} />
      ) : (
        <LoadingState />
      )}

      {data && (data.totalPages > 1 || page > 1) ? (
        <Pagination
          page={page}
          totalPages={Math.max(data.totalPages, page)}
          onPrevious={() => change({ search, page: page - 1 })}
          onNext={() => change({ search, page: page + 1 })}
        />
      ) : null}
    </ScrollView>
  );
}
