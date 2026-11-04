import { Pressable, StyleSheet, Text, View } from 'react-native';

type EmptyStateProps = {
  message: string;
  action?: { label: string; onPress: () => void };
};

export function EmptyState({ message, action }: EmptyStateProps) {
  return (
    <View style={styles.container}>
      <Text style={styles.message}>{message}</Text>
      {action ? (
        <Pressable accessibilityRole="button" onPress={action.onPress} style={styles.button}>
          <Text style={styles.buttonText}>{action.label}</Text>
        </Pressable>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { padding: 16, gap: 12, alignItems: 'flex-start' },
  message: { fontSize: 16 },
  button: {
    minHeight: 44,
    minWidth: 44,
    paddingHorizontal: 16,
    justifyContent: 'center',
    borderWidth: 1,
    borderRadius: 8,
  },
  buttonText: { fontSize: 16, fontWeight: '600' },
});
