import { Stack, useLocalSearchParams, useRouter } from 'expo-router';
import { FlatList, Text, useWindowDimensions, View } from 'react-native';

import { EmptyState } from '@/components/EmptyState';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { userMessage } from '@/lib/api-client';

import { useCategoriesQuery } from '../hooks/useCategoriesQuery';
import { useProductsQuery } from '../hooks/useProductsQuery';
import { nextListParams, parseListParams, PRODUCT_PAGE_SIZE, showingRange } from '../list-params';
import type { ProductListParams } from '../types';
import { CategoryFilter } from './CategoryFilter';
import { Pagination } from './Pagination';
import { ProductCard } from './ProductCard';
import { SearchBar } from './SearchBar';
import { SortPicker } from './SortPicker';

type UrlChange = Partial<Pick<ProductListParams, 'search' | 'category' | 'sort' | 'page'>>;

function columnsFor(width: number): number {
  if (width >= 900) return 4;
  if (width >= 600) return 3;
  return 2;
}

/** Every key is always present so router.setParams removes the ones that are no longer set. */
function toUrlParams(p: ProductListParams) {
  return {
    search: p.search,
    category: p.category,
    sort: p.sort,
    page: p.page > 1 ? String(p.page) : undefined,
  };
}

export function ProductListView() {
  const router = useRouter();
  const raw = useLocalSearchParams<{ search?: string; category?: string; sort?: string; page?: string }>();
  const params = parseListParams(raw);
  const { width } = useWindowDimensions();
  const columns = columnsFor(width);

  const products = useProductsQuery(params);
  const categories = useCategoriesQuery();

  function change(update: UrlChange) {
    router.setParams(toUrlParams(nextListParams(params, update)));
  }

  function clearFilters() {
    router.setParams(toUrlParams({ page: 1, pageSize: PRODUCT_PAGE_SIZE }));
  }

  const data = products.data;

  return (
    <View className="w-full max-w-content flex-1 gap-sm self-center p-lg">
      <Stack.Screen options={{ title: 'Products' }} />
      <Text accessibilityRole="header" className="text-heading font-semibold text-foreground">
        Products
      </Text>

      <SearchBar value={params.search ?? ''} onSearch={(search) => change({ search })} />
      {categories.data ? (
        <CategoryFilter categories={categories.data.items} selected={params.category} onSelect={(category) => change({ category })} />
      ) : null}
      <View className="flex-row items-center gap-sm">
        <Text className="text-small text-muted">Sort</Text>
        <View className="min-w-0 flex-1">
          <SortPicker sort={params.sort} hasSearch={!!params.search} onSelect={(sort) => change({ sort })} />
        </View>
      </View>

      {data ? (
        data.items.length > 0 ? (
          <>
            <View accessibilityLiveRegion="polite" className="flex-row gap-xs">
              <Text className="text-small text-muted">{`Showing ${showingRange(data).first}–${showingRange(data).last} of`}</Text>
              <Text className="text-small text-muted">{data.totalCount === 1 ? '1 product' : `${data.totalCount} products`}</Text>
            </View>
            <FlatList
              key={`columns-${columns}`}
              data={data.items}
              keyExtractor={(item) => item.id}
              numColumns={columns}
              columnWrapperClassName={columns > 1 ? 'gap-lg' : undefined}
              contentContainerClassName="gap-lg pb-lg"
              renderItem={({ item }) => (
                <View className="flex-1">
                  <ProductCard item={item} />
                </View>
              )}
              ListFooterComponent={
                <Pagination
                  page={data.page}
                  totalPages={data.totalPages}
                  onPrevious={() => change({ page: data.page - 1 })}
                  onNext={() => change({ page: data.page + 1 })}
                />
              }
            />
          </>
        ) : data.totalCount > 0 ? (
          <EmptyState
            message="There are no products on that page."
            action={{ label: 'Go to first page', onPress: () => change({ page: 1 }) }}
          />
        ) : (
          <EmptyState message="No products match your search." action={{ label: 'Clear filters', onPress: clearFilters }} />
        )
      ) : products.isError ? (
        <ErrorState message={userMessage(products.error)} onRetry={() => void products.refetch()} />
      ) : (
        <LoadingState />
      )}
    </View>
  );
}
