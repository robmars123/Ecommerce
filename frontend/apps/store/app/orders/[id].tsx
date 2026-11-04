import { useLocalSearchParams } from 'expo-router';
import { ScreenStub } from '@/components/ScreenStub';

export default function OrderDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <ScreenStub title="Order" detail={id} />;
}
