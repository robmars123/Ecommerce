import { useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';

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
    <View style={styles.container}>
      <Text accessibilityRole="header" style={styles.heading}>
        New category
      </Text>
      <TextField label="Category name" value={name} onChangeText={setName} error={fieldErrors.name?.[0]} />
      <TextField label="Category slug" value={slug} onChangeText={setSlug} error={fieldErrors.slug?.[0]} autoCapitalize="none" />
      <TextField label="Parent category slug" value={parentSlug} onChangeText={setParentSlug} error={fieldErrors.parentSlug?.[0]} autoCapitalize="none" />
      {create.error && Object.keys(fieldErrors).length === 0 ? (
        <Text accessibilityRole="alert" style={styles.error}>
          {userMessage(create.error)}
        </Text>
      ) : null}
      {create.isSuccess ? <Text style={styles.notice}>Category created.</Text> : null}
      <ActionButton label="Add category" onPress={submit} pending={create.isPending} />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { gap: 12 },
  heading: { fontSize: 18, fontWeight: '600' },
  notice: { fontSize: 16 },
  error: { fontSize: 16, color: '#b91c1c' },
});
