import { useState } from 'react';
import { Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { TextField } from '@/components/TextField';
import { ApiError, userMessage } from '@/lib/api-client';

import { useCreateCategoryMutation } from '../hooks/useCreateCategoryMutation';

export function CategoryCreateForm() {
  const create = useCreateCategoryMutation();
  const [name, setName] = useState('');
  const [slug, setSlug] = useState('');
  const [parentSlug, setParentSlug] = useState('');
  const fieldErrors = create.error instanceof ApiError ? (create.error.fieldErrors ?? {}) : {};

  function submit() {
    create.mutate(
      { name: name.trim(), slug: slug.trim(), parentSlug: parentSlug.trim() || null },
      {
        onSuccess: () => {
          setName('');
          setSlug('');
          setParentSlug('');
        },
      },
    );
  }

  return (
    <View className="gap-md rounded-card border border-border bg-surface p-lg">
      <Text accessibilityRole="header" className="text-title font-semibold text-foreground">
        New category
      </Text>
      <TextField label="Category name" value={name} onChangeText={setName} error={fieldErrors.name?.[0]} />
      <TextField label="Category slug" value={slug} onChangeText={setSlug} error={fieldErrors.slug?.[0]} autoCapitalize="none" />
      <TextField
        label="Parent category slug"
        value={parentSlug}
        onChangeText={setParentSlug}
        error={fieldErrors.parentSlug?.[0]}
        autoCapitalize="none"
      />
      {create.error && Object.keys(fieldErrors).length === 0 ? (
        <Text accessibilityRole="alert" className="text-body text-danger">
          {userMessage(create.error)}
        </Text>
      ) : null}
      {create.isSuccess ? <Text className="text-body text-foreground">Category created.</Text> : null}
      <ActionButton label="Add category" onPress={submit} pending={create.isPending} />
    </View>
  );
}
