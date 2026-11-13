import { useRef, useState } from 'react';
import { Pressable, Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { TextField } from '@/components/TextField';
import { ProductImageView } from '@/features/catalog/components/ProductImageView';
import { ApiError, userMessage } from '@/lib/api-client';
import { pickImageFile } from '@/lib/platform';

import { newRowKey, type ImageFormRow } from '../form-model';
import { useUploadProductImageMutation } from '../hooks/useUploadProductImageMutation';

export const SAVE_FIRST_HINT = 'Save the product first to add images.';
export const SAVE_TO_KEEP_HINT = 'Save the product to keep new images.';

type ImageKeysEditorProps = {
  /** The product to upload to. Not set on the create screen: the upload route needs an existing product. */
  productId?: string;
  rows: ImageFormRow[];
  onChange: (rows: ImageFormRow[]) => void;
  errorFor: (path: string) => string | undefined;
};

/**
 * Images are picked from the device and uploaded one at a time; the server answers with the key and a URL. The key goes into the
 * next product save. Order in the list is the display order. Progress is indeterminate (fetch reports none).
 */
export function ImageKeysEditor({ productId, rows, onChange, errorFor }: ImageKeysEditorProps) {
  const upload = useUploadProductImageMutation();
  const [uploadError, setUploadError] = useState<string>();
  // The upload can take a while: add the row to what the form holds when it finishes, not to what it held when it started.
  const latestRows = useRef(rows);
  latestRows.current = rows;

  function update(index: number, patch: Partial<ImageFormRow>) {
    onChange(rows.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  }

  async function addImage() {
    if (!productId) {
      return;
    }
    setUploadError(undefined);
    const form = await pickImageFile();
    if (!form) {
      return;
    }
    try {
      const image = await upload.mutateAsync({ id: productId, form });
      onChange([...latestRows.current, { key: newRowKey(), storageKey: image.storageKey, altText: '', url: image.url }]);
    } catch (error) {
      const fileError = error instanceof ApiError ? error.fieldErrors?.file?.[0] : undefined;
      setUploadError(fileError ?? userMessage(error));
    }
  }

  return (
    <View className="gap-md">
      <Text accessibilityRole="header" className="text-title font-semibold text-foreground">
        Images
      </Text>
      {rows.map((row, i) => {
        const keyError = errorFor(`images[${i}].storageKey`);
        return (
          <View key={row.key} className="gap-sm rounded-control border border-border bg-surface-raised p-md">
            <ProductImageView url={row.url} altText={row.altText} fallbackLabel={`Image ${i + 1}`} width={96} />
            {keyError ? (
              <Text accessibilityRole="alert" className="text-body text-danger">
                {keyError}
              </Text>
            ) : null}
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
        );
      })}
      {uploadError ? (
        <Text accessibilityRole="alert" className="text-body text-danger">
          {uploadError}
        </Text>
      ) : null}
      <ActionButton
        label={upload.isPending ? 'Uploading…' : 'Add image'}
        onPress={() => void addImage()}
        disabled={!productId || upload.isPending}
      />
      <Text className="text-body text-muted">{productId ? SAVE_TO_KEEP_HINT : SAVE_FIRST_HINT}</Text>
    </View>
  );
}
