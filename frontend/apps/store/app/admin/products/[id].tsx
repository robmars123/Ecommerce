import { useLocalSearchParams } from 'expo-router';
import { ScreenStub } from '@/components/ScreenStub';

export default function AdminProductDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <ScreenStub title="Admin · Product" detail={id} />;
}
