import { Pressable, StyleSheet, Text, View } from 'react-native';

type PaginationProps = {
  page: number;
  totalPages: number;
  onPrevious: () => void;
  onNext: () => void;
};

export function Pagination({ page, totalPages, onPrevious, onNext }: PaginationProps) {
  if (totalPages <= 1) {
    return null;
  }
  const atStart = page <= 1;
  const atEnd = page >= totalPages;

  return (
    <View style={styles.row}>
      <Pressable
        accessibilityRole="button"
        accessibilityState={{ disabled: atStart }}
        disabled={atStart}
        onPress={onPrevious}
        style={[styles.button, atStart && styles.disabled]}
      >
        <Text style={styles.text}>Previous</Text>
      </Pressable>
      <Text style={styles.text}>{`Page ${page} of ${totalPages}`}</Text>
      <Pressable
        accessibilityRole="button"
        accessibilityState={{ disabled: atEnd }}
        disabled={atEnd}
        onPress={onNext}
        style={[styles.button, atEnd && styles.disabled]}
      >
        <Text style={styles.text}>Next</Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingVertical: 12 },
  button: {
    minHeight: 44,
    minWidth: 44,
    paddingHorizontal: 16,
    justifyContent: 'center',
    alignItems: 'center',
    borderWidth: 1,
    borderRadius: 8,
  },
  disabled: { opacity: 0.4 },
  text: { fontSize: 16 },
});
