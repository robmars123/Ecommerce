import { View } from 'react-native';

import { TextField } from '@/components/TextField';

import type { AddressInput } from '../types';

export const EMPTY_ADDRESS: AddressInput = { fullName: '', line1: '', line2: '', city: '', region: '', postalCode: '', country: '' };

type AddressFormProps = {
  value: AddressInput;
  onChange: (next: AddressInput) => void;
  /** Server field errors by wire name, such as `shippingAddress.fullName`. */
  errors: Record<string, string>;
  editable: boolean;
};

/** The inline shipping address (arch 9). Plain inputs: the server validates and snapshots it. */
export function AddressForm({ value, onChange, errors, editable }: AddressFormProps) {
  const field = (name: keyof AddressInput, label: string, extra: { autoComplete?: 'name' | 'postal-code' | 'country' } = {}) => (
    <TextField
      label={label}
      value={value[name]}
      onChangeText={(text) => onChange({ ...value, [name]: text })}
      error={errors[`shippingAddress.${name}`]}
      editable={editable}
      {...extra}
    />
  );
  return (
    <View className="gap-md">
      {field('fullName', 'Full name', { autoComplete: 'name' })}
      {field('line1', 'Address line 1')}
      {field('line2', 'Address line 2 (optional)')}
      {field('city', 'City')}
      {field('region', 'State or region (optional)')}
      {field('postalCode', 'Postal code', { autoComplete: 'postal-code' })}
      {field('country', 'Country (2-letter code, such as US)')}
    </View>
  );
}
