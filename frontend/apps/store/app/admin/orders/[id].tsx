import { useLocalSearchParams } from 'expo-router';
import { ScreenStub } from '@/components/ScreenStub';

export default function AdminOrderDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <ScreenStub title="Admin · Order" detail={id} />;
}
