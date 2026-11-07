import { Link, useRouter } from 'expo-router';
import { useState } from 'react';
import { Pressable, Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { TextField } from '@/components/TextField';
import { ApiError, userMessage } from '@/lib/api-client';

import { GuestCartNotice } from '@/features/cart/components/GuestCartNotice';

import { useRegisterMutation } from '../hooks/useRegisterMutation';
import { PasswordChecklist } from './PasswordChecklist';

export const EMAIL_TAKEN_COPY = 'An account with this email already exists.';
export const PASSWORD_MISMATCH_COPY = 'Passwords do not match.';

export function RegisterForm() {
  const router = useRouter();
  const register = useRegisterMutation();
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [mismatch, setMismatch] = useState(false);

  const error = register.error;
  const fieldErrors = error instanceof ApiError ? error.fieldErrors : undefined;
  const emailTaken = error instanceof ApiError && error.kind === 'conflict';

  function submit() {
    if (password !== confirm) {
      setMismatch(true);
      return;
    }
    setMismatch(false);
    register.mutate(
      { displayName: displayName.trim(), email: email.trim(), password },
      { onSuccess: () => router.replace('/') },
    );
  }

  return (
    <View className="gap-md">
      <GuestCartNotice />
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
        secureTextEntry={!showPassword}
        autoCapitalize="none"
        autoComplete="new-password"
      />
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={showPassword ? 'Hide password' : 'Show password'}
        onPress={() => setShowPassword((v) => !v)}
        className="min-h-touch justify-center self-start"
      >
        <Text className="text-body font-semibold text-primary">{showPassword ? 'Hide' : 'Show'}</Text>
      </Pressable>
      <PasswordChecklist value={password} />
      <TextField
        label="Confirm password"
        value={confirm}
        onChangeText={setConfirm}
        error={mismatch && password !== confirm ? PASSWORD_MISMATCH_COPY : undefined}
        secureTextEntry={!showPassword}
        autoCapitalize="none"
        autoComplete="new-password"
      />
      {emailTaken ? (
        <Text accessibilityRole="alert" className="text-body text-danger">
          {EMAIL_TAKEN_COPY}
        </Text>
      ) : error && !fieldErrors ? (
        <Text accessibilityRole="alert" className="text-body text-danger">
          {userMessage(error)}
        </Text>
      ) : null}
      <ActionButton label="Create account" onPress={submit} pending={register.isPending} />
      <Text className="text-small text-muted">By creating an account you agree to Northline's terms of service and privacy policy.</Text>
      <Link href="/login" asChild>
        <Text className="min-h-touch py-md text-body text-foreground">I already have an account</Text>
      </Link>
    </View>
  );
}
