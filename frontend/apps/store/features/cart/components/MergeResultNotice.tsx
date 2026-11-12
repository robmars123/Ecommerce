import { Link } from 'expo-router';
import { Pressable, Text, View } from 'react-native';

import { mergeResultText } from '../cart-messages';
import { useMergeResultStore } from '../merge-result-store';

/** The merge outcome, global so it shows wherever sign-in returns to. Every line, with its reason; never dropped silently. */
export function MergeResultNotice() {
  const results = useMergeResultStore((s) => s.results);
  const failed = useMergeResultStore((s) => s.failed);
  const retry = useMergeResultStore((s) => s.retry);
  const dismiss = useMergeResultStore((s) => s.dismiss);

  if (failed) {
    return (
      <View accessibilityRole="alert" className="border-b border-border bg-surface">
        <View testID="merge-notice-content" className="w-full max-w-content gap-sm self-center px-lg py-sm">
          <Text className="text-body text-danger">
            We could not add the cart on this device to your account yet. It is still saved on this device.
          </Text>
          <Pressable
            accessibilityRole="button"
            onPress={() => retry?.()}
            className="min-h-touch min-w-touch justify-center self-start rounded-control border border-border px-lg"
          >
            <Text className="text-body font-semibold text-foreground">Try again</Text>
          </Pressable>
        </View>
      </View>
    );
  }
  if (!results) {
    return null;
  }
  return (
    <View accessibilityRole="summary" className="border-b border-border bg-surface">
      <View testID="merge-notice-content" className="w-full max-w-content gap-sm self-center px-lg py-sm">
        <Text className="text-body font-semibold text-foreground">The cart on this device was added to your account</Text>
        {results.map((r) => (
          <Text key={r.variantId} className="text-body text-foreground">
            {`${r.name}: ${mergeResultText(r)}`}
          </Text>
        ))}
        <View className="flex-row gap-md">
          <Link href="/cart" asChild>
            <Pressable
              accessibilityRole="link"
              className="min-h-touch min-w-touch justify-center rounded-control border border-border px-lg"
            >
              <Text className="text-body font-semibold text-foreground">View cart</Text>
            </Pressable>
          </Link>
          <Pressable
            accessibilityRole="button"
            onPress={dismiss}
            className="min-h-touch min-w-touch justify-center rounded-control border border-border px-lg"
          >
            <Text className="text-body font-semibold text-foreground">Dismiss</Text>
          </Pressable>
        </View>
      </View>
    </View>
  );
}
