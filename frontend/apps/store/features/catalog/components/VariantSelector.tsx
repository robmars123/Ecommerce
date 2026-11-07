import { Pressable, Text, View } from 'react-native';

import type { ProductVariant } from '../types';

type VariantSelectorProps = {
  variants: ProductVariant[];
  selectedId: string | undefined;
  onSelect: (variantId: string) => void;
};

export function variantLabel(variant: ProductVariant): string {
  return variant.name || [variant.size, variant.colour].filter(Boolean).join(' ') || variant.sku;
}

/** "Size" when every variant has a size, otherwise the neutral "Option". */
export function variantGroupLabel(variants: ProductVariant[]): 'Size' | 'Option' {
  return variants.length > 0 && variants.every((v) => Boolean(v.size)) ? 'Size' : 'Option';
}

export function VariantSelector({ variants, selectedId, onSelect }: VariantSelectorProps) {
  const selectedVariant = variants.find((v) => v.id === selectedId);
  return (
    <View className="gap-sm">
      <Text className="text-small font-semibold text-foreground">
        {selectedVariant
          ? `${variantGroupLabel(variants)}: ${variantLabel(selectedVariant)}`
          : variantGroupLabel(variants)}
      </Text>
      <View className="flex-row flex-wrap gap-sm">
        {variants.map((v) => {
          const selected = v.id === selectedId;
          const soldOut = v.stockStatus === 'Out';
          return (
            <Pressable
              key={v.id}
              accessibilityRole="button"
              accessibilityState={{ selected, disabled: soldOut }}
              accessibilityHint={soldOut ? 'Sold out' : undefined}
              disabled={soldOut}
              onPress={() => onSelect(v.id)}
              className={`min-h-touch min-w-touch items-center justify-center rounded-control border px-lg ${selected ? 'border-selected bg-selected' : 'border-border bg-surface-raised'} ${soldOut ? 'opacity-50' : ''}`}
            >
              <Text
                className={`text-body ${selected ? 'font-semibold text-selected-foreground' : 'text-foreground'} ${soldOut ? 'line-through' : ''}`}
              >
                {variantLabel(v)}
              </Text>
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}
