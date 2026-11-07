import { Link, useRouter } from 'expo-router';
import { useState } from 'react';
import { Text, View } from 'react-native';

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
    <View className="gap-md">
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
          <Text accessibilityRole="alert" className="text-body text-danger">
            {RESET_LINK_INVALID_COPY}
          </Text>
          <Link href={{ pathname: '/reset-password' }} asChild>
            <Text className="min-h-touch py-md text-body text-foreground">Request a new link</Text>
          </Link>
        </>
      ) : error && !passwordError ? (
        <Text accessibilityRole="alert" className="text-body text-danger">
          {userMessage(error)}
        </Text>
      ) : null}
      <ActionButton label="Change password" onPress={submit} pending={confirm.isPending} />
    </View>
  );
}
