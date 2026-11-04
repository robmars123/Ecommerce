import { useLocalSearchParams } from 'expo-router';

import { ProductDetailView } from '@/features/catalog/components/ProductDetailView';

export default function ProductDetailScreen() {
  const { slug } = useLocalSearchParams<{ slug: string }>();
  return <ProductDetailView slug={slug} />;
}
