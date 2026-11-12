import { useQuery } from '@tanstack/react-query';
import { Link, usePathname } from 'expo-router';
import { useEffect, useState } from 'react';
import { Pressable, Text, TextInput, View } from 'react-native';

import { useDebouncedValue } from '@/features/catalog/hooks/useDebouncedValue';
import type { UserRole } from '@/features/auth/types';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { colors } from '@/lib/design-tokens';
import { queryKeys } from '@/lib/query-keys';

import { fetchAdminSearch } from '../api';
import { ADMIN_SEARCH_DEBOUNCE_MS, searchGroups, searchTerm } from '../search';

type ResultsProps = { term: string; role: UserRole; onNavigate: () => void };

function SearchResults({ term, role, onNavigate }: ResultsProps) {
  const query = useQuery({ queryKey: queryKeys.admin.search(term), queryFn: ({ signal }) => fetchAdminSearch(term, signal) });

  if (query.isPending) return <LoadingState />;
  if (query.isError) return <ErrorState message="Search isn't available right now." onRetry={() => void query.refetch()} />;

  const groups = searchGroups(role, query.data);
  if (groups.length === 0) return <Text className="px-lg py-md text-body text-muted">{`No matches for “${term}”.`}</Text>;
  return (
    <View className="gap-md py-md">
      {groups.map((group) => (
        <View key={group.title} className="gap-xs">
          <Text accessibilityRole="header" className="px-lg text-caption text-muted">
            {group.title}
          </Text>
          {group.items.map((item) => (
            <Link key={item.key} href={item.href as never} asChild>
              <Pressable accessibilityRole="link" onPress={onNavigate} className="min-h-touch justify-center px-lg">
                <Text className="text-body text-foreground">{item.label}</Text>
              </Pressable>
            </Link>
          ))}
        </View>
      ))}
    </View>
  );
}

/** Top-bar search with an inline results panel. The role filter only hides groups; the API enforces the roles. */
export function AdminSearch({ role }: { role: UserRole }) {
  const [text, setText] = useState('');
  const [isOpen, setOpen] = useState(true);
  const pathname = usePathname();
  const term = searchTerm(useDebouncedValue(text, ADMIN_SEARCH_DEBOUNCE_MS));

  function reset() {
    setText('');
    setOpen(true);
  }

  useEffect(reset, [pathname]);

  const showPanel = isOpen && text.trim() !== '';

  return (
    <View className="w-full gap-xs">
      <View className="flex-row items-center gap-xs">
        <TextInput
          accessibilityLabel="Search admin"
          placeholder="Search"
          value={text}
          onChangeText={(next) => {
            setText(next);
            setOpen(true);
          }}
          onKeyPress={(e) => {
            if (e.nativeEvent.key === 'Escape') setOpen(false);
          }}
          autoCapitalize="none"
          autoCorrect={false}
          maxLength={100}
          placeholderTextColor={colors.muted}
          className="min-h-touch flex-1 rounded-input border border-border bg-surface px-lg text-body text-foreground"
        />
        {text ? (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Clear search"
            onPress={reset}
            className="min-h-touch min-w-touch items-center justify-center"
          >
            <Text className="text-body text-foreground">Clear</Text>
          </Pressable>
        ) : null}
      </View>
      {showPanel ? (
        <View accessibilityLabel="Search results" className="rounded-input border border-border bg-surface">
          {term === null ? (
            <Text className="px-lg py-md text-body text-muted">Type at least 2 characters.</Text>
          ) : (
            <SearchResults term={term} role={role} onNavigate={reset} />
          )}
        </View>
      ) : null}
    </View>
  );
}
