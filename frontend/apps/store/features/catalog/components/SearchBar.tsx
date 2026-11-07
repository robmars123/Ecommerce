import { useEffect, useRef, useState } from 'react';
import { Pressable, StyleSheet, Text, TextInput, View } from 'react-native';

import { colors } from '@/lib/design-tokens';

import { useDebouncedValue } from '../hooks/useDebouncedValue';

export const SEARCH_DEBOUNCE_MS = 300;

type SearchBarProps = {
  /** The committed search (for example from the URL). Changes from outside replace the typed text. */
  value?: string;
  /** Called with the trimmed text once typing has paused. */
  onSearch?: (text: string) => void;
  /** Called immediately when the user submits (keyboard search key). */
  onSubmit?: (text: string) => void;
  debounceMs?: number;
};

export function SearchBar({ value = '', onSearch, onSubmit, debounceMs = SEARCH_DEBOUNCE_MS }: SearchBarProps) {
  const [text, setText] = useState(value);
  const lastEmitted = useRef(value.trim());
  const debounced = useDebouncedValue(text, debounceMs);

  useEffect(() => {
    const next = debounced.trim();
    if (next !== lastEmitted.current) {
      lastEmitted.current = next;
      onSearch?.(next);
    }
    // onSearch is intentionally not a dependency: only a settled text change should emit.
  }, [debounced]);

  useEffect(() => {
    if (value.trim() !== lastEmitted.current) {
      lastEmitted.current = value.trim();
      setText(value);
    }
  }, [value]);

  function clear() {
    setText('');
    if (lastEmitted.current !== '') {
      lastEmitted.current = '';
      onSearch?.('');
    }
  }

  return (
    <View className="flex-row items-center gap-sm">
      <TextInput
        accessibilityLabel="Search products"
        placeholder="Search products"
        value={text}
        onChangeText={setText}
        onSubmitEditing={() => onSubmit?.(text.trim())}
        returnKeyType="search"
        autoCapitalize="none"
        autoCorrect={false}
        maxLength={100}
        placeholderTextColor={colors.muted}
        className="min-h-touch rounded-input border border-border bg-surface px-lg text-body text-foreground"
        style={styles.input}
      />
      {text ? (
        <Pressable accessibilityRole="button" accessibilityLabel="Clear search" onPress={clear} className="min-h-touch min-w-touch items-center justify-center">
          <Text className="text-body text-foreground">Clear</Text>
        </Pressable>
      ) : null}
    </View>
  );
}

// className does not resolve under Jest, so the asserted flex properties stay a style (arch 5.5 fallback).
const styles = StyleSheet.create({
  input: { flex: 1, minWidth: 0 },
});
