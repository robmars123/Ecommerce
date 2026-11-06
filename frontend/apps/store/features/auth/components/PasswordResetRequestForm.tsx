import { useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { TextField } from '@/components/TextField';
import { ApiError, userMessage } from '@/lib/api-client';

import { usePasswordResetRequestMutation } from '../hooks/usePasswordResetRequestMutation';

// The same words for every email, so the form never reveals which accounts exist.
export const RESET_REQUESTED_COPY = 'If an account exists for that email, we have sent a link to reset the password.';

export function PasswordResetRequestForm() {
  const request = usePasswordResetRequestMutation();
  const [email, setEmail] = useState('');
  const fieldErrors = request.error instanceof ApiError ? request.error.fieldErrors : undefined;

  return (
    <View style={styles.container}>
      <TextField
        label="Email"
        value={email}
        onChangeText={setEmail}
        error={fieldErrors?.email?.[0]}
        keyboardType="email-address"
        autoCapitalize="none"
        autoComplete="email"
      />
      {request.isSuccess ? <Text style={styles.notice}>{RESET_REQUESTED_COPY}</Text> : null}
      {request.error && !fieldErrors ? (
        <Text accessibilityRole="alert" style={styles.error}>
          {userMessage(request.error)}
        </Text>
      ) : null}
      <ActionButton label="Send reset link" onPress={() => request.mutate(email.trim())} pending={request.isPending} />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { gap: 12 },
  notice: { fontSize: 16 },
  error: { fontSize: 16, color: '#b91c1c' },
});
