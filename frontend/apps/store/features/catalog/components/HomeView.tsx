import { Link, Stack, useRouter } from 'expo-router';
import { StyleSheet, Text, View } from 'react-native';

import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { AccountBar } from '@/features/auth/components/AccountBar';
import { userMessage } from '@/lib/api-client';

import { useCategoriesQuery } from '../hooks/useCategoriesQuery';
import { SearchBar } from './SearchBar';

export function HomeView() {
  const router = useRouter();
  const categories = useCategoriesQuery();

  function search(text: string) {
    if (text) {
      router.push({ pathname: '/products', params: { search: text } });
    } else {
      router.push('/products');
    }
  }

  return (
    <View style={styles.container}>
      <Stack.Screen options={{ title: 'Home' }} />
      <Text accessibilityRole="header" style={styles.title}>
        Home
      </Text>

      <AccountBar />

      <SearchBar onSubmit={search} />

      <Text style={styles.subtitle}>Shop by category</Text>
      {categories.data ? (
        <View style={styles.links}>
          {categories.data.items.map((c) => (
            <Link
              key={c.slug}
              href={{ pathname: '/products', params: { category: c.slug } }}
              accessibilityRole="link"
              style={styles.link}
            >
              {c.name}
            </Link>
          ))}
        </View>
      ) : categories.isError ? (
        <ErrorState message={userMessage(categories.error)} onRetry={() => void categories.refetch()} />
      ) : (
        <LoadingState />
      )}

      <Link href="/products" accessibilityRole="link" style={styles.link}>
        Browse all products
      </Link>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, padding: 16, gap: 12 },
  title: { fontSize: 24, fontWeight: '600' },
  subtitle: { fontSize: 18, fontWeight: '600' },
  links: { gap: 4 },
  link: { minHeight: 44, paddingVertical: 12, fontSize: 16 },
});
