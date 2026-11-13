import { Pressable, Text } from 'react-native';

type ActionButtonProps = {
  label: string;
  onPress: () => void;
  disabled?: boolean;
  /** A request is in flight: the button is disabled so a double tap cannot send twice. */
  pending?: boolean;
  /** The rounded pill look (admin Refresh). */
  pill?: boolean;
};

export function ActionButton({ label, onPress, disabled, pending, pill }: ActionButtonProps) {
  const isDisabled = Boolean(disabled || pending);
  const shape = pill ? 'rounded-pill web:shadow-accent active:scale-[0.98]' : 'rounded-control';
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled: isDisabled, busy: Boolean(pending) }}
      disabled={isDisabled}
      onPress={onPress}
      className={`min-h-touch min-w-touch max-w-full items-center justify-center ${shape} border border-primary bg-primary px-lg ${isDisabled ? 'opacity-50' : ''}`}
    >
      <Text className={`${pill ? 'text-ui' : 'text-body'} font-semibold text-primary-foreground`}>{label}</Text>
    </Pressable>
  );
}
