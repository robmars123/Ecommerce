import { Link, useRouter } from 'expo-router';
import { useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { TextField } from '@/components/TextField';
import { ApiError, userMessage } from '@/lib/api-client';

import { usePasswordResetConfirmMutation } from '../hooks/usePasswordResetConfirmMutation';

export const RESET_LINK_INVALID_COPY = 'This reset link is invalid or has expired.';

export function PasswordResetConfirmForm({ token }: { token: string }) {
  const router = useRouter();
  const confirm = usePasswordResetConfirmMutation();
  const [newPassword, setNewPassword] = useState('');

  const error = confirm.error;
  const passwordError = error instanceof ApiError ? error.fieldErrors?.newPassword?.[0] : undefined;
  const linkInvalid = error instanceof ApiError && error.kind === 'validation' && !passwordError;

  function submit() {
    confirm.mutate(
      { token, newPassword },
      { onSuccess: () => router.replace({ pathname: '/login', params: { notice: 'password-changed' } }) },
    );
  }

  return (
    <View style={styles.container}>
      <TextField
        label="New password"
        value={newPassword}
        onChangeText={setNewPassword}
        error={passwordError}
        secureTextEntry
        autoCapitalize="none"
        autoComplete="new-password"
      />
      {linkInvalid ? (
        <>
          <Text accessibilityRole="alert" style={styles.error}>
            {RESET_LINK_INVALID_COPY}
          </Text>
          <Link href={{ pathname: '/reset-password' }} style={styles.link}>
            Request a new link
          </Link>
        </>
      ) : error && !passwordError ? (
        <Text accessibilityRole="alert" style={styles.error}>
          {userMessage(error)}
        </Text>
      ) : null}
      <ActionButton label="Change password" onPress={submit} pending={confirm.isPending} />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { gap: 12 },
  error: { fontSize: 16, color: '#b91c1c' },
  link: { minHeight: 44, paddingVertical: 12, fontSize: 16 },
});
