import { Pressable, StyleSheet, Text } from 'react-native';

type ActionButtonProps = {
  label: string;
  onPress: () => void;
  disabled?: boolean;
  /** A request is in flight: the button is disabled so a double tap cannot send twice. */
  pending?: boolean;
};

export function ActionButton({ label, onPress, disabled, pending }: ActionButtonProps) {
  const isDisabled = Boolean(disabled || pending);
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled: isDisabled, busy: Boolean(pending) }}
      disabled={isDisabled}
      onPress={onPress}
      style={[styles.button, isDisabled && styles.disabled]}
    >
      <Text style={styles.text}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  button: {
    minHeight: 44,
    minWidth: 44,
    paddingHorizontal: 16,
    justifyContent: 'center',
    alignItems: 'center',
    borderWidth: 1,
    borderRadius: 8,
  },
  disabled: { opacity: 0.5 },
  text: { fontSize: 16, fontWeight: '600' },
});
