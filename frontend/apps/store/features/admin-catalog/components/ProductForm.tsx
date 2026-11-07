import { useState } from 'react';
import { Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { TextField } from '@/components/TextField';
import { ApiError, userMessage } from '@/lib/api-client';

import type { ProductFormState } from '../form-model';
import { ImageKeysEditor } from './ImageKeysEditor';
import { VariantRowsEditor } from './VariantRowsEditor';

export const STALE_VERSION_COPY = 'This product was changed by someone else. Reload and try again.';

type ProductFormProps = {
  mode: 'create' | 'edit';
  initial: ProductFormState;
  onSubmit: (state: ProductFormState) => void;
  pending: boolean;
  error: unknown;
  /** Edit only: refetch the product and reset the form to what the server has. */
  onReload?: () => void;
};

export function ProductForm({ mode, initial, onSubmit, pending, error, onReload }: ProductFormProps) {
  const [state, setState] = useState(initial);
  const isEdit = mode === 'edit';

  const fieldErrors = error instanceof ApiError ? (error.fieldErrors ?? {}) : {};
  const errorFor = (path: string) => fieldErrors[path]?.[0];
  const isStale = error instanceof ApiError && error.kind === 'conflict' && Boolean(fieldErrors.version);
  const shown = new Set(['slug', 'name', 'description', 'categorySlug', 'currency']);
  const unplaced = Object.entries(fieldErrors)
    .filter(([key]) => !shown.has(key) && !key.startsWith('variants') && !key.startsWith('images') && key !== 'version')
    .flatMap(([, messages]) => messages);

  function set<K extends keyof ProductFormState>(key: K, value: ProductFormState[K]) {
    setState((current) => ({ ...current, [key]: value }));
  }

  return (
    <View className="gap-md">
      {isStale ? (
        <View className="gap-sm rounded-control border border-border p-md">
          <Text accessibilityRole="alert" className="text-body text-danger">
            {STALE_VERSION_COPY}
          </Text>
          {onReload ? <ActionButton label="Reload" onPress={onReload} /> : null}
        </View>
      ) : error && Object.keys(fieldErrors).length === 0 ? (
        <Text accessibilityRole="alert" className="text-body text-danger">
          {userMessage(error)}
        </Text>
      ) : null}
      {unplaced.map((message) => (
        <Text key={message} accessibilityRole="alert" className="text-body text-danger">
          {message}
        </Text>
      ))}

      <TextField label="Name" value={state.name} onChangeText={(v) => set('name', v)} error={errorFor('name')} />
      <TextField label="Slug" value={state.slug} onChangeText={(v) => set('slug', v)} error={errorFor('slug')} autoCapitalize="none" />
      <TextField label="Description" value={state.description} onChangeText={(v) => set('description', v)} error={errorFor('description')} multiline />
      <TextField
        label="Category slug"
        value={state.categorySlug}
        onChangeText={(v) => set('categorySlug', v)}
        error={errorFor('categorySlug')}
        autoCapitalize="none"
      />
      <TextField
        label="Currency"
        value={state.currency}
        onChangeText={(v) => set('currency', v)}
        error={errorFor('currency')}
        autoCapitalize="characters"
        editable={!isEdit}
      />

      <VariantRowsEditor rows={state.variants} onChange={(rows) => set('variants', rows)} isEdit={isEdit} errorFor={errorFor} />
      <ImageKeysEditor rows={state.images} onChange={(rows) => set('images', rows)} errorFor={errorFor} />

      <ActionButton label={isEdit ? 'Save changes' : 'Create product'} onPress={() => onSubmit(state)} pending={pending} />
    </View>
  );
}
