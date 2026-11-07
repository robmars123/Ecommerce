import { ScrollView, Text, View } from 'react-native';

import { ScreenTitle } from '@/components/ScreenTitle';
import { RegisterForm } from '@/features/auth/components/RegisterForm';

const BENEFITS = [
  { title: 'Secure checkout', body: 'Prices and totals are always calculated on our servers.' },
  { title: 'One account', body: 'Sign in from the web or the app with the same email and password.' },
  { title: 'No spam', body: 'We only use your email for sign-in and account security.' },
];

/** Two columns on wide screens, stacked on phones. */
export default function RegisterScreen() {
  return (
    <ScrollView contentContainerClassName="p-lg" keyboardShouldPersistTaps="handled">
      <View className="w-full max-w-content gap-xl self-center lg:flex-row">
        <View className="gap-md lg:flex-1">
          <Text accessibilityRole="header" className="text-display font-bold text-foreground">
            Create your Northline account
          </Text>
          <Text className="text-body text-muted">One account for checking out and managing your details.</Text>
          {BENEFITS.map((b) => (
            <View key={b.title} className="gap-xs">
              <Text className="text-body font-semibold text-foreground">{b.title}</Text>
              <Text className="text-small text-muted">{b.body}</Text>
            </View>
          ))}
        </View>
        <View className="gap-md rounded-card border border-border bg-surface p-lg lg:flex-1">
          <ScreenTitle title="Register" />
          <RegisterForm />
        </View>
      </View>
    </ScrollView>
  );
}
