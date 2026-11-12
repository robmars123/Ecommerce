import { ActivityIndicator, View } from 'react-native';

import { useThemeColors } from '@/lib/theme';

export function LoadingState() {
  const colors = useThemeColors();
  return (
    <View className="items-center p-xl">
      <ActivityIndicator accessibilityLabel="Loading" size="large" color={colors.primary} />
    </View>
  );
}
