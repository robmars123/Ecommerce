import { Pressable, ScrollView, StyleSheet, Text } from 'react-native';

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
      contentContainerStyle={styles.row}
    >
      {options.map((option) => (
        <Pressable
          key={option}
          accessibilityRole="button"
          accessibilityState={{ selected: option === active }}
          onPress={() => onSelect(option)}
          style={[styles.chip, option === active && styles.chipSelected]}
        >
          <Text style={[styles.chipText, option === active && styles.chipTextSelected]}>{LABELS[option]}</Text>
        </Pressable>
      ))}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  scroller: { flexGrow: 0, flexShrink: 0 },
  row: { gap: 8, paddingVertical: 4 },
  chip: {
    minHeight: 44,
    minWidth: 44,
    paddingHorizontal: 16,
    justifyContent: 'center',
    alignItems: 'center',
    borderWidth: 1,
    borderRadius: 22,
  },
  chipSelected: { backgroundColor: '#111827' },
  chipText: { fontSize: 16 },
  chipTextSelected: { color: '#ffffff', fontWeight: '600' },
});
