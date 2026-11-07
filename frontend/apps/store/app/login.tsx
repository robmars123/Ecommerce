import { View } from 'react-native';

import { ScreenTitle } from '@/components/ScreenTitle';
import { LoginForm } from '@/features/auth/components/LoginForm';

/** Sign-in dialog: a card over the dimmed page behind it. */
export default function LoginScreen() {
  return (
    <View className="flex-1 items-center justify-center bg-black/60 p-lg">
      <View className="w-full max-w-md gap-md rounded-card border border-border bg-surface p-lg">
        <ScreenTitle title="Sign in" />
        <LoginForm />
      </View>
    </View>
  );
}
