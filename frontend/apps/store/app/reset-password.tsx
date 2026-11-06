import { useLocalSearchParams } from 'expo-router';

import { FormScreen } from '@/components/FormScreen';
import { PasswordResetConfirmForm } from '@/features/auth/components/PasswordResetConfirmForm';
import { PasswordResetRequestForm } from '@/features/auth/components/PasswordResetRequestForm';

export default function ResetPasswordScreen() {
  const { token } = useLocalSearchParams<{ token?: string }>();
  const value = Array.isArray(token) ? token[0] : token;

  return (
    <FormScreen title="Reset password">
      {value ? <PasswordResetConfirmForm token={value} /> : <PasswordResetRequestForm />}
    </FormScreen>
  );
}
