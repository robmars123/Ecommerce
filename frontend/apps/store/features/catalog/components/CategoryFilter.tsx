import { Pressable, ScrollView, StyleSheet, Text } from 'react-native';

import type { Category } from '../types';

type CategoryFilterProps = {
  categories: Category[];
  selected?: string;
  onSelect: (slug: string | undefined) => void;
};

export function CategoryFilter({ categories, selected, onSelect }: CategoryFilterProps) {
  return (
    <ScrollView
      horizontal
      testID="category-scroller"
      showsHorizontalScrollIndicator={false}
      style={styles.scroller}
      contentContainerStyle={styles.row}
    >
      <Chip label="All" selected={selected === undefined} onPress={() => onSelect(undefined)} />
      {categories.map((c) => (
        <Chip key={c.slug} label={c.name} selected={selected === c.slug} onPress={() => onSelect(c.slug)} />
      ))}
    </ScrollView>
  );
}

function Chip({ label, selected, onPress }: { label: string; selected: boolean; onPress: () => void }) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ selected }}
      onPress={onPress}
      style={[styles.chip, selected && styles.chipSelected]}
    >
      <Text style={[styles.chipText, selected && styles.chipTextSelected]}>{label}</Text>
    </Pressable>
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
