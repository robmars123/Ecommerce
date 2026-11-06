import { Link, useRouter } from 'expo-router';
import { useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { TextField } from '@/components/TextField';
import { ApiError, userMessage } from '@/lib/api-client';

import { useRegisterMutation } from '../hooks/useRegisterMutation';

export const EMAIL_TAKEN_COPY = 'An account with this email already exists.';

export function RegisterForm() {
  const router = useRouter();
  const register = useRegisterMutation();
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  const error = register.error;
  const fieldErrors = error instanceof ApiError ? error.fieldErrors : undefined;
  const emailTaken = error instanceof ApiError && error.kind === 'conflict';

  function submit() {
    register.mutate(
      { displayName: displayName.trim(), email: email.trim(), password },
      { onSuccess: () => router.replace('/') },
    );
  }

  return (
    <View style={styles.container}>
      <TextField
        label="Name"
        value={displayName}
        onChangeText={setDisplayName}
        error={fieldErrors?.displayName?.[0]}
        autoComplete="name"
      />
      <TextField
        label="Email"
        value={email}
        onChangeText={setEmail}
        error={fieldErrors?.email?.[0]}
        keyboardType="email-address"
        autoCapitalize="none"
        autoComplete="email"
      />
      <TextField
        label="Password"
        value={password}
        onChangeText={setPassword}
        error={fieldErrors?.password?.[0]}
        secureTextEntry
        autoCapitalize="none"
        autoComplete="new-password"
      />
      {emailTaken ? (
        <Text accessibilityRole="alert" style={styles.error}>
          {EMAIL_TAKEN_COPY}
        </Text>
      ) : error && !fieldErrors ? (
        <Text accessibilityRole="alert" style={styles.error}>
          {userMessage(error)}
        </Text>
      ) : null}
      <ActionButton label="Register" onPress={submit} pending={register.isPending} />
      <Link href="/login" style={styles.link}>
        I already have an account
      </Link>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { gap: 12 },
  error: { fontSize: 16, color: '#b91c1c' },
  link: { minHeight: 44, paddingVertical: 12, fontSize: 16 },
});
