import { Pressable, StyleSheet, Switch, Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { TextField } from '@/components/TextField';

import { emptyVariantRow, type VariantFormRow } from '../form-model';

type VariantRowsEditorProps = {
  rows: VariantFormRow[];
  onChange: (rows: VariantFormRow[]) => void;
  /** Edit mode shows the Active switch and cannot remove a saved variant (the server forbids removal by omission). */
  isEdit: boolean;
  errorFor: (path: string) => string | undefined;
};

export function VariantRowsEditor({ rows, onChange, isEdit, errorFor }: VariantRowsEditorProps) {
  function update(index: number, patch: Partial<VariantFormRow>) {
    onChange(rows.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  }

  return (
    <View style={styles.container}>
      <Text accessibilityRole="header" style={styles.heading}>
        Variants
      </Text>
      {errorFor('variants') ? (
        <Text accessibilityRole="alert" style={styles.error}>
          {errorFor('variants')}
        </Text>
      ) : null}
      {rows.map((row, i) => {
        const n = i + 1;
        const at = (field: string) => errorFor(`variants[${i}].${field}`);
        return (
          <View key={row.key} style={styles.row}>
            <TextField label={`Variant ${n} SKU`} value={row.sku} onChangeText={(sku) => update(i, { sku })} error={at('sku')} autoCapitalize="characters" />
            <TextField label={`Variant ${n} name`} value={row.name} onChangeText={(name) => update(i, { name })} error={at('name')} />
            <TextField label={`Variant ${n} size`} value={row.size} onChangeText={(size) => update(i, { size })} error={at('size')} />
            <TextField label={`Variant ${n} colour`} value={row.colour} onChangeText={(colour) => update(i, { colour })} error={at('colour')} />
            <TextField
              label={`Variant ${n} price`}
              value={row.price}
              onChangeText={(price) => update(i, { price })}
              error={at('price.amount') ?? at('price')}
              keyboardType="decimal-pad"
            />
            <TextField
              label={`Variant ${n} compare-at price`}
              value={row.compareAt}
              onChangeText={(compareAt) => update(i, { compareAt })}
              error={at('compareAtPrice.amount') ?? at('compareAtPrice')}
              keyboardType="decimal-pad"
            />
            <TextField
              label={`Variant ${n} weight (grams)`}
              value={row.weightGrams}
              onChangeText={(weightGrams) => update(i, { weightGrams })}
              error={at('weightGrams')}
              keyboardType="number-pad"
            />
            {isEdit ? (
              <View style={styles.switchRow}>
                <Text style={styles.label}>{`Variant ${n} active`}</Text>
                <Switch
                  accessibilityLabel={`Variant ${n} active`}
                  value={row.isActive}
                  onValueChange={(isActive) => update(i, { isActive })}
                />
              </View>
            ) : null}
            {!row.id ? (
              <Pressable
                accessibilityRole="button"
                accessibilityLabel={`Remove variant ${n}`}
                onPress={() => onChange(rows.filter((_, index) => index !== i))}
                style={styles.remove}
              >
                <Text style={styles.label}>Remove</Text>
              </Pressable>
            ) : null}
          </View>
        );
      })}
      <ActionButton label="Add variant" onPress={() => onChange([...rows, emptyVariantRow()])} />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { gap: 12 },
  heading: { fontSize: 18, fontWeight: '600' },
  row: { gap: 8, padding: 12, borderWidth: 1, borderRadius: 8 },
  switchRow: { flexDirection: 'row', alignItems: 'center', gap: 12, minHeight: 44 },
  label: { fontSize: 16 },
  remove: { minHeight: 44, minWidth: 44, justifyContent: 'center' },
  error: { fontSize: 14, color: '#b91c1c' },
});
