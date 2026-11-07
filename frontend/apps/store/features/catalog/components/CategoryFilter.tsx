import { Pressable, ScrollView, StyleSheet, Text } from 'react-native';

import { touch } from '@/lib/design-tokens';

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
      contentContainerClassName="gap-sm py-xs"
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
      className={`min-w-touch items-center justify-center rounded-pill border px-lg ${selected ? 'border-selected bg-selected' : 'border-border bg-surface'}`}
      style={styles.touch}
    >
      <Text className={`text-body ${selected ? 'font-semibold text-selected-foreground' : 'text-foreground'}`}>{label}</Text>
    </Pressable>
  );
}

// className does not resolve under Jest, so the asserted properties stay styles (arch §5.5 fallback).
const styles = StyleSheet.create({
  scroller: { flexGrow: 0, flexShrink: 0 },
  touch: { minHeight: touch },
});
