import { Image } from 'expo-image';
import { StyleSheet, View } from 'react-native';

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

export function ProductImageView({
  url,
  altText,
  fallbackLabel,
  aspectRatio = 1,
  width,
  maxWidth,
}: ProductImageViewProps) {
  const label = altText || fallbackLabel;
  const box = { aspectRatio, width: width ?? ('100%' as const), maxWidth };

  if (!url) {
    return <View accessible accessibilityRole="image" accessibilityLabel={label} style={[styles.placeholder, box]} />;
  }

  return (
    <View style={box}>
      <Image
        accessible
        accessibilityRole="image"
        accessibilityLabel={label}
        source={{ uri: url }}
        contentFit="cover"
        transition={150}
        style={styles.image}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  placeholder: { backgroundColor: '#e5e7eb', borderRadius: 8 },
  image: { width: '100%', height: '100%', borderRadius: 8 },
});
