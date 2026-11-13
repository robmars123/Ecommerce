import { Text, View } from 'react-native';

import type { StatusTone } from '../status-labels';

const TONE_CLASSES: Record<StatusTone, { box: string; dot: string; text: string }> = {
  warning: { box: 'border-warning-border bg-warning-subtle', dot: 'bg-warning', text: 'text-warning' },
  success: { box: 'border-primary-subtle bg-primary-subtle', dot: 'bg-primary-text', text: 'text-primary-text' },
  danger: { box: 'border-danger-border bg-danger-subtle', dot: 'bg-danger', text: 'text-danger' },
  neutral: { box: 'border-border bg-background', dot: 'bg-muted', text: 'text-muted' },
};

/** A tone-coloured pill with a dot: the dot is decoration, the label carries the meaning. */
export function StatusPill({ tone, label }: { tone: StatusTone; label: string }) {
  const classes = TONE_CLASSES[tone];
  return (
    <View className={`flex-row items-center gap-sm self-start rounded-pill border px-md py-xs ${classes.box}`}>
      <View importantForAccessibility="no" className={`h-[7px] w-[7px] rounded-pill ${classes.dot}`} />
      <Text className={`text-ui-sm font-bold ${classes.text}`}>{label}</Text>
    </View>
  );
}
