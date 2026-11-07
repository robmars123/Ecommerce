import { Pressable, ScrollView, StyleSheet, Text } from 'react-native';

import { touch } from '@/lib/design-tokens';

import type { ProductSort } from '../types';

const LABELS: Record<ProductSort, string> = {
  relevance: 'Relevance',
  newest: 'Newest',
  'price-asc': 'Price: low to high',
  'price-desc': 'Price: high to low',
};

type SortPickerProps = {
  sort?: ProductSort;
  hasSearch: boolean;
  onSelect: (sort: ProductSort) => void;
};

export function SortPicker({ sort, hasSearch, onSelect }: SortPickerProps) {
  const options: ProductSort[] = hasSearch
    ? ['relevance', 'newest', 'price-asc', 'price-desc']
    : ['newest', 'price-asc', 'price-desc'];
  const active = sort ?? (hasSearch ? 'relevance' : 'newest');

  return (
    <ScrollView
      horizontal
      testID="sort-scroller"
      showsHorizontalScrollIndicator={false}
      style={styles.scroller}
      contentContainerClassName="gap-sm py-xs"
    >
      {options.map((option) => (
        <Pressable
          key={option}
          accessibilityRole="button"
          accessibilityState={{ selected: option === active }}
          onPress={() => onSelect(option)}
          className={`min-w-touch items-center justify-center rounded-pill border px-lg ${option === active ? 'border-selected bg-selected' : 'border-border bg-surface'}`}
          style={styles.touch}
        >
          <Text className={`text-body ${option === active ? 'font-semibold text-selected-foreground' : 'text-foreground'}`}>
            {LABELS[option]}
          </Text>
        </Pressable>
      ))}
    </ScrollView>
  );
}

// className does not resolve under Jest, so the asserted properties stay styles (arch §5.5 fallback).
const styles = StyleSheet.create({
  scroller: { flexGrow: 0, flexShrink: 0 },
  touch: { minHeight: touch },
});
