import { Pressable, Text, View } from 'react-native';

type EmptyStateProps = {
  message: string;
  action?: { label: string; onPress: () => void };
};

export function EmptyState({ message, action }: EmptyStateProps) {
  return (
    <View className="items-start gap-md p-lg">
      <Text className="text-body text-foreground">{message}</Text>
      {action ? (
        <Pressable
          accessibilityRole="button"
          onPress={action.onPress}
          className="min-h-touch min-w-touch justify-center rounded-control border border-border px-lg"
        >
          <Text className="text-body font-semibold text-foreground">{action.label}</Text>
        </Pressable>
      ) : null}
    </View>
  );
}
