import { Pressable, Text, View } from 'react-native';

import { touch } from '@/lib/design-tokens';

type ErrorStateProps = {
  message: string;
  onRetry?: () => void;
};

// The touch target stays a token-sourced style: className does not resolve under Jest (arch §5.5 fallback).
export function ErrorState({ message, onRetry }: ErrorStateProps) {
  return (
    <View className="items-start gap-md p-lg">
      <Text accessibilityRole="alert" className="text-body text-danger">
        {message}
      </Text>
      {onRetry ? (
        <Pressable
          accessibilityRole="button"
          onPress={onRetry}
          className="justify-center rounded-control border border-border px-lg"
          style={{ minHeight: touch, minWidth: touch }}
        >
          <Text className="text-body font-semibold text-foreground">Try again</Text>
        </Pressable>
      ) : null}
    </View>
  );
}
