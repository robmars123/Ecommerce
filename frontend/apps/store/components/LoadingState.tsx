import { ActivityIndicator, View } from 'react-native';

import { colors } from '@/lib/design-tokens';

export function LoadingState() {
  return (
    <View className="items-center p-xl">
      <ActivityIndicator accessibilityLabel="Loading" size="large" color={colors.primary} />
    </View>
  );
}
