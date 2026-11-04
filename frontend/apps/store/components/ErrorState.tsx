import { Pressable, StyleSheet, Text, View } from 'react-native';

type ErrorStateProps = {
  message: string;
  onRetry?: () => void;
};

export function ErrorState({ message, onRetry }: ErrorStateProps) {
  return (
    <View style={styles.container}>
      <Text accessibilityRole="alert" style={styles.message}>
        {message}
      </Text>
      {onRetry ? (
        <Pressable accessibilityRole="button" onPress={onRetry} style={styles.button}>
          <Text style={styles.buttonText}>Try again</Text>
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
