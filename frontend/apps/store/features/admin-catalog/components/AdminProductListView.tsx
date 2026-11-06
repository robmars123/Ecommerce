import { Link, useLocalSearchParams, useRouter } from 'expo-router';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';

import { EmptyState } from '@/components/EmptyState';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { ScreenTitle } from '@/components/ScreenTitle';
import { CategoryFilter } from '@/features/catalog/components/CategoryFilter';
import { Pagination } from '@/features/catalog/components/Pagination';
import { SearchBar } from '@/features/catalog/components/SearchBar';
import { useCategoriesQuery } from '@/features/catalog/hooks/useCategoriesQuery';
import { userMessage } from '@/lib/api-client';
import { formatMoney } from '@/lib/money';

import { useAdminProductsQuery } from '../hooks/useAdminProductsQuery';
import type { AdminProductListParams, AdminStatus } from '../types';
import { CategoryCreateForm } from './CategoryCreateForm';

const PAGE_SIZE = 20;
const STATUSES: ReadonlyArray<{ value: AdminStatus; label: string }> = [
  { value: 'all', label: 'All' },
  { value: 'active', label: 'Active' },
  { value: 'inactive', label: 'Not published' },
];

type RawParams = { search?: string; category?: string; status?: string; page?: string };

function parseParams(raw: RawParams): AdminProductListParams {
  const search = raw.search?.trim().slice(0, 100);
  const category = raw.category?.trim();
  const status = STATUSES.find((s) => s.value === raw.status)?.value;
  const page = raw.page && /^\d+$/.test(raw.page) && Number(raw.page) >= 1 ? Number(raw.page) : 1;
  return {
    ...(search ? { search } : {}),
    ...(category ? { category } : {}),
    ...(status && status !== 'all' ? { status } : {}),
    page,
    pageSize: PAGE_SIZE,
  };
}

export function AdminProductListView() {
  const router = useRouter();
  const raw = useLocalSearchParams<RawParams>();
  const params = parseParams(raw);
  const products = useAdminProductsQuery(params);
  const categories = useCategoriesQuery();

  /** Every key is always present so router.setParams removes the ones that are no longer set. */
  function change(update: Partial<Pick<AdminProductListParams, 'search' | 'category' | 'status' | 'page'>>) {
    const next = { ...params, page: 1, ...update };
    router.setParams({
      search: next.search || undefined,
      category: next.category || undefined,
      status: next.status && next.status !== 'all' ? next.status : undefined,
      page: next.page > 1 ? String(next.page) : undefined,
    });
  }

  const data = products.data;

  return (
    <ScrollView contentContainerStyle={styles.container}>
      <ScreenTitle title="Admin · Products" />
      <Link href="/admin/products/new" accessibilityRole="link" style={styles.link}>
        Add product
      </Link>

      <SearchBar value={params.search ?? ''} onSearch={(text) => change({ search: text || undefined })} />
      <View style={styles.statuses}>
        {STATUSES.map((s) => {
          const selected = (params.status ?? 'all') === s.value;
          return (
            <Pressable
              key={s.value}
              accessibilityRole="button"
              accessibilityState={{ selected }}
              onPress={() => change({ status: s.value })}
              style={[styles.chip, selected && styles.chipSelected]}
            >
              <Text style={styles.chipText}>{s.label}</Text>
            </Pressable>
          );
        })}
      </View>
      {categories.data ? (
        <CategoryFilter
          categories={categories.data.items}
          selected={params.category}
          onSelect={(slug) => change({ category: slug })}
        />
      ) : null}

      {data ? (
        data.items.length === 0 ? (
          <EmptyState message="No products match." />
        ) : (
          <View style={styles.list}>
            {data.items.map((item) => (
              <Link
                key={item.id}
                href={{ pathname: '/admin/products/[id]', params: { id: item.id } }}
                accessibilityRole="link"
                style={styles.row}
              >
                {`${item.name} · ${item.isActive ? 'Active' : 'Not published'} · ${item.activeVariantCount}/${item.variantCount} variants active${item.price ? ` · ${formatMoney(item.price)}` : ''}`}
              </Link>
            ))}
          </View>
        )
      ) : products.isError ? (
        <ErrorState message={userMessage(products.error)} onRetry={() => void products.refetch()} />
      ) : (
        <LoadingState />
      )}

      {data ? (
        <Pagination
          page={data.page}
          totalPages={data.totalPages}
          onPrevious={() => change({ page: data.page - 1 })}
          onNext={() => change({ page: data.page + 1 })}
        />
      ) : null}

      <CategoryCreateForm />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { padding: 16, gap: 12 },
  link: { minHeight: 44, paddingVertical: 12, fontSize: 16 },
  statuses: { flexDirection: 'row', gap: 8, flexWrap: 'wrap' },
  chip: { minHeight: 44, minWidth: 44, paddingHorizontal: 12, justifyContent: 'center', borderWidth: 1, borderRadius: 8 },
  chipSelected: { borderWidth: 2 },
  chipText: { fontSize: 16 },
  list: { gap: 4 },
  row: { minHeight: 44, paddingVertical: 12, fontSize: 16 },
});
