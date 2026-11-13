import { Image } from 'expo-image';
import { StyleSheet, View } from 'react-native';

import { resolveImageUrl } from '@/lib/config';

type ProductImageViewProps = {
  url: string | null;
  altText: string;
  /** Used as the accessible label when altText is empty. */
  fallbackLabel: string;
  /** Explicit box so layout never shifts while the image loads. */
  aspectRatio?: number;
  width?: number;
  maxWidth?: number;
};

export function ProductImageView({ url, altText, fallbackLabel, aspectRatio = 1, width, maxWidth }: ProductImageViewProps) {
  const label = altText || fallbackLabel;
  // Runtime values from the API or the caller: an allowed style exception (arch §5.5).
  const box = { aspectRatio, width: width ?? ('100%' as const), maxWidth };
  const source = resolveImageUrl(url);

  if (!source) {
    return (
      <View accessible accessibilityRole="image" accessibilityLabel={label} className="rounded-control bg-surface-raised" style={box} />
    );
  }

  return (
    <View className="overflow-hidden rounded-control bg-surface-raised" style={box}>
      <Image
        accessible
        accessibilityRole="image"
        accessibilityLabel={label}
        source={{ uri: source }}
        contentFit="cover"
        transition={150}
        style={StyleSheet.absoluteFill}
      />
    </View>
  );
}
