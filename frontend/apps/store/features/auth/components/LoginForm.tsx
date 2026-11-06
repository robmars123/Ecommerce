import { Link, useLocalSearchParams, useRouter } from 'expo-router';
import { useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { TextField } from '@/components/TextField';
import { ApiError, userMessage } from '@/lib/api-client';

import { useLoginMutation } from '../hooks/useLoginMutation';
import { safeReturnTo } from '../return-to';

export const LOGIN_FAILED_COPY = 'Email or password is incorrect, or the account is temporarily locked.';
export const PASSWORD_CHANGED_COPY = 'Your password was changed. Please log in.';

export function LoginForm() {
  const router = useRouter();
  const { returnTo, notice } = useLocalSearchParams<{ returnTo?: string; notice?: string }>();
  const login = useLoginMutation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  const error = login.error;
  const fieldErrors = error instanceof ApiError ? error.fieldErrors : undefined;

  function submit() {
    login.mutate(
      { email: email.trim(), password },
      { onSuccess: () => router.replace(safeReturnTo(returnTo) as never) },
    );
  }

  return (
    <View style={styles.container}>
      {notice === 'password-changed' ? <Text style={styles.notice}>{PASSWORD_CHANGED_COPY}</Text> : null}
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
        autoComplete="current-password"
      />
      {error && !fieldErrors ? (
        <Text accessibilityRole="alert" style={styles.error}>
          {error instanceof ApiError && error.kind === 'unauthorized' ? LOGIN_FAILED_COPY : userMessage(error)}
        </Text>
      ) : null}
      <ActionButton label="Log in" onPress={submit} pending={login.isPending} />
      <Link href="/reset-password" style={styles.link}>
        Forgot your password?
      </Link>
      <Link href="/register" style={styles.link}>
        Create an account
      </Link>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { gap: 12 },
  notice: { fontSize: 16 },
  error: { fontSize: 16, color: '#b91c1c' },
  link: { minHeight: 44, paddingVertical: 12, fontSize: 16 },
});
