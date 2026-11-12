import { Link, useLocalSearchParams, useRouter } from 'expo-router';
import { useState } from 'react';
import { Pressable, Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { TextField } from '@/components/TextField';
import { ApiError, userMessage } from '@/lib/api-client';

import { GuestCartNotice } from '@/features/cart/components/GuestCartNotice';

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
    login.mutate({ email: email.trim(), password }, { onSuccess: () => router.replace(safeReturnTo(returnTo) as never) });
  }

  function cancel() {
    if (router.canGoBack()) {
      router.back();
    } else {
      router.replace('/');
    }
  }

  return (
    <View className="gap-md">
      <GuestCartNotice />
      {notice === 'password-changed' ? <Text className="text-body text-foreground">{PASSWORD_CHANGED_COPY}</Text> : null}
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
        <Text accessibilityRole="alert" className="text-body text-danger">
          {error instanceof ApiError && error.kind === 'unauthorized' ? LOGIN_FAILED_COPY : userMessage(error)}
        </Text>
      ) : null}
      <ActionButton label="Sign in" onPress={submit} pending={login.isPending} />
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Cancel"
        onPress={cancel}
        className="min-h-touch items-center justify-center"
      >
        <Text className="text-body font-semibold text-foreground">Cancel</Text>
      </Pressable>
      <Link href="/reset-password" asChild>
        <Text className="min-h-touch py-md text-body text-foreground">Forgot your password?</Text>
      </Link>
      <Link href="/register" asChild>
        <Text className="min-h-touch py-md text-body text-foreground">Create an account</Text>
      </Link>
    </View>
  );
}
