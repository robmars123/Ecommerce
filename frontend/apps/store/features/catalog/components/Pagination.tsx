import { Pressable, Text, View } from 'react-native';

type PaginationProps = {
  page: number;
  totalPages: number;
  onPrevious: () => void;
  onNext: () => void;
};

const BUTTON = 'min-h-touch min-w-touch items-center justify-center rounded-control border border-border bg-surface px-lg';

export function Pagination({ page, totalPages, onPrevious, onNext }: PaginationProps) {
  if (totalPages <= 1) {
    return null;
  }
  const atStart = page <= 1;
  const atEnd = page >= totalPages;

  return (
    <View className="flex-row items-center justify-between py-md">
      <Pressable
        accessibilityRole="button"
        accessibilityState={{ disabled: atStart }}
        disabled={atStart}
        onPress={onPrevious}
        className={`${BUTTON} ${atStart ? 'opacity-40' : ''}`}
      >
        <Text className="text-body text-foreground">Previous</Text>
      </Pressable>
      <Text className="text-body text-foreground">{`Page ${page} of ${totalPages}`}</Text>
      <Pressable
        accessibilityRole="button"
        accessibilityState={{ disabled: atEnd }}
        disabled={atEnd}
        onPress={onNext}
        className={`${BUTTON} ${atEnd ? 'opacity-40' : ''}`}
      >
        <Text className="text-body text-foreground">Next</Text>
      </Pressable>
    </View>
  );
}
