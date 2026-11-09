import { useEffect, useState } from 'react';
import { Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { TextField } from '@/components/TextField';
import type { Category } from '@/features/catalog/types';
import { ApiError, userMessage } from '@/lib/api-client';

import { useDeleteCategoryMutation } from '../hooks/useDeleteCategoryMutation';
import { useUpdateCategoryMutation } from '../hooks/useUpdateCategoryMutation';

export type CategoryRowMode = 'edit' | 'delete' | null;

type CategoryRowProps = {
  category: Category;
  parentName: string | null;
  mode: CategoryRowMode;
  onOpen: (mode: 'edit' | 'delete') => void;
  onClose: () => void;
};

function fieldErrorsOf(error: unknown): Record<string, string[]> {
  return error instanceof ApiError ? (error.fieldErrors ?? {}) : {};
}

export function CategoryRow({ category, parentName, mode, onOpen, onClose }: CategoryRowProps) {
  const update = useUpdateCategoryMutation();
  const remove = useDeleteCategoryMutation();
  const [name, setName] = useState(category.name);
  const [slug, setSlug] = useState(category.slug);

  // Prefill when the editor opens and drop any stale error when the row closes.
  useEffect(() => {
    setName(category.name);
    setSlug(category.slug);
    update.reset();
    remove.reset();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mode]);

  const updateErrors = fieldErrorsOf(update.error);
  const deleteErrors = fieldErrorsOf(remove.error);
  const deleteMessage = deleteErrors.products?.[0] ?? deleteErrors.subcategories?.[0];

  function save() {
    // mutateAsync's promise settles even after the refetch unmounts this row, unlike a per-call mutate callback.
    update.mutateAsync({ currentSlug: category.slug, body: { slug: slug.trim(), name: name.trim() } }).then(onClose, () => undefined);
  }

  function confirmDelete() {
    remove.mutateAsync(category.slug).then(onClose, () => undefined);
  }

  return (
    <View className="gap-sm border-b border-border py-md">
      <Text className="text-body font-semibold text-foreground">{`${category.name} · ${category.slug}`}</Text>
      <Text className="text-small text-muted">{parentName ? `Under ${parentName}` : category.parentSlug ? `Under ${category.parentSlug}` : 'Top level'}</Text>

      {mode === null ? (
        <View className="flex-row flex-wrap gap-md">
          <ActionButton label={`Edit ${category.name}`} onPress={() => onOpen('edit')} />
          <ActionButton label={`Delete ${category.name}`} onPress={() => onOpen('delete')} />
        </View>
      ) : null}

      {mode === 'edit' ? (
        <View className="gap-md">
          <TextField label={`New name for ${category.name}`} value={name} onChangeText={setName} error={updateErrors.name?.[0]} />
          <TextField label={`New slug for ${category.name}`} value={slug} onChangeText={setSlug} error={updateErrors.slug?.[0]} autoCapitalize="none" />
          {update.error && !updateErrors.name && !updateErrors.slug ? (
            <Text accessibilityRole="alert" className="text-body text-danger">
              {userMessage(update.error)}
            </Text>
          ) : null}
          <View className="flex-row flex-wrap gap-md">
            <ActionButton label="Save" onPress={save} pending={update.isPending} />
            <ActionButton label="Cancel" onPress={onClose} />
          </View>
        </View>
      ) : null}

      {mode === 'delete' ? (
        <View className="gap-md">
          <Text className="text-body text-foreground">{`Delete ${category.name}? Products and subcategories must be moved first.`}</Text>
          {remove.error ? (
            <Text accessibilityRole="alert" className="text-body text-danger">
              {deleteMessage ?? userMessage(remove.error)}
            </Text>
          ) : null}
          <View className="flex-row flex-wrap gap-md">
            <ActionButton label="Confirm delete" onPress={confirmDelete} pending={remove.isPending} />
            <ActionButton label="Cancel" onPress={onClose} />
          </View>
        </View>
      ) : null}
    </View>
  );
}
