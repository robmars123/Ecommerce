import { Pressable, Text } from 'react-native';

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
      className={`min-h-touch min-w-touch items-center justify-center rounded-control border border-primary bg-primary px-lg ${isDisabled ? 'opacity-50' : ''}`}
    >
      <Text className="text-body font-semibold text-primary-foreground">{label}</Text>
    </Pressable>
  );
}
