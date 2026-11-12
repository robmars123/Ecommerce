import { Pressable, Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { TextField } from '@/components/TextField';

import { newRowKey, type ImageFormRow } from '../form-model';

type ImageKeysEditorProps = {
  rows: ImageFormRow[];
  onChange: (rows: ImageFormRow[]) => void;
  errorFor: (path: string) => string | undefined;
};

/** Images are storage keys typed by the admin; there is no upload yet. Order in the list is the display order. */
export function ImageKeysEditor({ rows, onChange, errorFor }: ImageKeysEditorProps) {
  function update(index: number, patch: Partial<ImageFormRow>) {
    onChange(rows.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  }

  return (
    <View className="gap-md">
      <Text accessibilityRole="header" className="text-title font-semibold text-foreground">
        Images
      </Text>
      {rows.map((row, i) => (
        <View key={row.key} className="gap-sm rounded-control border border-border bg-surface-raised p-md">
          <TextField
            label={`Image ${i + 1} storage key`}
            value={row.storageKey}
            onChangeText={(storageKey) => update(i, { storageKey })}
            error={errorFor(`images[${i}].storageKey`)}
            autoCapitalize="none"
          />
          <TextField
            label={`Image ${i + 1} description`}
            value={row.altText}
            onChangeText={(altText) => update(i, { altText })}
            error={errorFor(`images[${i}].altText`)}
          />
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={`Remove image ${i + 1}`}
            onPress={() => onChange(rows.filter((_, index) => index !== i))}
            className="min-h-touch min-w-touch justify-center"
          >
            <Text className="text-body text-foreground">Remove</Text>
          </Pressable>
        </View>
      ))}
      <ActionButton label="Add image" onPress={() => onChange([...rows, { key: newRowKey(), storageKey: '', altText: '' }])} />
    </View>
  );
}
