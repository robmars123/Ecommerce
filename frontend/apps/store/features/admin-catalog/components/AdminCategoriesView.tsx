import { useState } from 'react';
import { ScrollView, Text, View } from 'react-native';

import { EmptyState } from '@/components/EmptyState';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { ScreenTitle } from '@/components/ScreenTitle';
import { useCategoriesQuery } from '@/features/catalog/hooks/useCategoriesQuery';
import { userMessage } from '@/lib/api-client';

import { CategoryCreateForm } from './CategoryCreateForm';
import { CategoryRow, type CategoryRowMode } from './CategoryRow';

type Open = { slug: string; mode: 'edit' | 'delete' } | null;

export function AdminCategoriesView() {
  const categories = useCategoriesQuery();
  const [open, setOpen] = useState<Open>(null);
  const items = categories.data?.items;

  return (
    <ScrollView contentContainerClassName="gap-md p-lg">
      <ScreenTitle title="Admin · Categories" />
      <CategoryCreateForm />
      <Text accessibilityRole="header" className="text-title font-semibold text-foreground">
        Categories
      </Text>
      {items ? (
        items.length === 0 ? (
          <EmptyState message="No categories yet." />
        ) : (
          <View>
            {items.map((c) => {
              const mode: CategoryRowMode = open?.slug === c.slug ? open.mode : null;
              return (
                <CategoryRow
                  key={c.slug}
                  category={c}
                  parentName={c.parentSlug ? (items.find((p) => p.slug === c.parentSlug)?.name ?? null) : null}
                  mode={mode}
                  onOpen={(m) => setOpen({ slug: c.slug, mode: m })}
                  onClose={() => setOpen(null)}
                />
              );
            })}
          </View>
        )
      ) : categories.isError ? (
        <ErrorState message={userMessage(categories.error)} onRetry={() => void categories.refetch()} />
      ) : (
        <LoadingState />
      )}
    </ScrollView>
  );
}
