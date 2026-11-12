import { useState } from 'react';
import { Text, TextInput, View, type TextInputProps } from 'react-native';

import { useThemeColors } from '@/lib/theme';

type TextFieldProps = {
  label: string;
  value: string;
  onChangeText: (text: string) => void;
  error?: string;
  secureTextEntry?: boolean;
  multiline?: boolean;
  keyboardType?: TextInputProps['keyboardType'];
  autoCapitalize?: TextInputProps['autoCapitalize'];
  autoComplete?: TextInputProps['autoComplete'];
  editable?: boolean;
};

/** A labelled input with its error under it. The label is the accessible name. */
export function TextField({ label, value, onChangeText, error, multiline, editable = true, ...inputProps }: TextFieldProps) {
  const [focused, setFocused] = useState(false);
  const colors = useThemeColors();
  return (
    <View className="gap-xs">
      <Text className="text-small font-semibold text-foreground">{label}</Text>
      <TextInput
        accessibilityLabel={label}
        value={value}
        onChangeText={onChangeText}
        editable={editable}
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        multiline={multiline}
        placeholderTextColor={colors.muted}
        className={`min-h-touch rounded-control border bg-surface px-md text-body text-foreground ${error ? 'border-danger' : focused ? 'border-primary' : 'border-border'} ${multiline ? 'min-h-textarea py-sm align-top' : ''}`}
        {...inputProps}
      />
      {error ? (
        <Text accessibilityRole="alert" className="text-small text-danger">
          {error}
        </Text>
      ) : null}
    </View>
  );
}
