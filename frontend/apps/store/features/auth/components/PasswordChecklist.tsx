import { Text, View } from 'react-native';

import { passwordRules } from '../password-rules';

/** The password rules with a met / not met state. The text carries the state, not colour alone. */
export function PasswordChecklist({ value }: { value: string }) {
  return (
    <View className="gap-xs">
      {passwordRules(value).map((rule) => (
        <Text key={rule.label} className={`text-small ${rule.met ? 'text-primary' : 'text-muted'}`}>
          {rule.met ? '✓' : '○'} {rule.label}
          <Text className="sr-only">{rule.met ? ' (met)' : ' (not met)'}</Text>
        </Text>
      ))}
    </View>
  );
}
