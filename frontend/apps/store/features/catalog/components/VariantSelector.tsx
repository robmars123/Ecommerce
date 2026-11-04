import { Pressable, StyleSheet, Text, View } from 'react-native';

import type { ProductVariant } from '../types';

type VariantSelectorProps = {
  variants: ProductVariant[];
  selectedId: string | undefined;
  onSelect: (variantId: string) => void;
};

export function variantLabel(variant: ProductVariant): string {
  return variant.name || [variant.size, variant.colour].filter(Boolean).join(' ') || variant.sku;
}

export function VariantSelector({ variants, selectedId, onSelect }: VariantSelectorProps) {
  return (
    <View style={styles.row}>
      {variants.map((v) => {
        const selected = v.id === selectedId;
        return (
          <Pressable
            key={v.id}
            accessibilityRole="button"
            accessibilityState={{ selected }}
            onPress={() => onSelect(v.id)}
            style={[styles.chip, selected && styles.chipSelected]}
          >
            <Text style={[styles.text, selected && styles.textSelected]}>{variantLabel(v)}</Text>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  chip: {
    minHeight: 44,
    minWidth: 44,
    paddingHorizontal: 16,
    justifyContent: 'center',
    alignItems: 'center',
    borderWidth: 1,
    borderRadius: 8,
  },
  chipSelected: { backgroundColor: '#111827' },
  text: { fontSize: 16 },
  textSelected: { color: '#ffffff', fontWeight: '600' },
});
