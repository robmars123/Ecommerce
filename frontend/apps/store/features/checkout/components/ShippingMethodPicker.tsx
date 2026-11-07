import { Pressable, Text, View } from 'react-native';

import { formatMoney } from '@/lib/money';

import type { ShippingMethod } from '../types';

type ShippingMethodPickerProps = {
  methods: ShippingMethod[];
  selected: string | null;
  onSelect: (code: string) => void;
  error?: string;
  editable: boolean;
};

/** Radio list of the configured flat-rate methods. The prices are the server's. */
export function ShippingMethodPicker({ methods, selected, onSelect, error, editable }: ShippingMethodPickerProps) {
  return (
    <View accessibilityRole="radiogroup" className="gap-sm">
      <Text accessibilityRole="header" className="text-title font-semibold text-foreground">
        Shipping method
      </Text>
      {methods.map((method) => {
        const label = `${method.name}, ${formatMoney(method.price)}, about ${method.estimatedDays} days`;
        const isSelected = method.code === selected;
        return (
          <Pressable
            key={method.code}
            accessibilityRole="radio"
            accessibilityLabel={label}
            accessibilityState={{ selected: isSelected, disabled: !editable }}
            disabled={!editable}
            onPress={() => onSelect(method.code)}
            className={`min-h-touch justify-center rounded-control border px-md ${isSelected ? 'border-primary' : 'border-border'}`}
          >
            <Text className="text-body text-foreground">{label}</Text>
          </Pressable>
        );
      })}
      {error ? (
        <Text accessibilityRole="alert" className="text-small text-danger">
          {error}
        </Text>
      ) : null}
    </View>
  );
}
