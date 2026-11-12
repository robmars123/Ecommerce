import { useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { AdminPanel } from '@/components/AdminPanel';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { ScreenTitle } from '@/components/ScreenTitle';
import { TextField } from '@/components/TextField';
import { formatOrderDate } from '@/features/orders/status-labels';
import { userMessage } from '@/lib/api-client';

import { useDecideReturnMutation, useReturnsQuery } from '../hooks/hooks';
import type { ReturnRequestRow, ReturnStatus } from '../types';

const FILTERS: (ReturnStatus | 'all')[] = ['Requested', 'Approved', 'Declined', 'Refunded', 'all'];

function ReturnCard({ request }: { request: ReturnRequestRow }) {
  const decide = useDecideReturnMutation();
  const [note, setNote] = useState('');
  const open = request.status === 'Requested';

  function act(decision: 'approve' | 'decline') {
    decide.mutate({ id: request.id, decision, reason: note.trim() });
  }

  return (
    <View className="gap-sm border-b border-border py-md">
      <Text className="text-body font-semibold text-foreground">{`${request.orderNumber} · ${request.status}`}</Text>
      <Text className="text-small text-muted">{formatOrderDate(request.createdAt)}</Text>
      {request.items.map((item) => (
        <Text key={item.variantId} className="text-body text-foreground">{`${item.name} × ${item.quantity}`}</Text>
      ))}
      <Text className="text-body text-foreground">{`Reason: ${request.reason}`}</Text>
      {request.adminNote ? <Text className="text-small text-muted">{`Note: ${request.adminNote}`}</Text> : null}
      {open ? (
        <View className="gap-sm rounded-control bg-surface-raised p-md">
          <TextField label={`Decision note for ${request.orderNumber}`} value={note} onChangeText={setNote} />
          {decide.error ? (
            <Text accessibilityRole="alert" className="text-body text-danger">
              {userMessage(decide.error)}
            </Text>
          ) : null}
          <View className="flex-row flex-wrap gap-sm">
            <ActionButton
              label={`Approve ${request.orderNumber}`}
              onPress={() => act('approve')}
              pending={decide.isPending}
              disabled={note.trim().length === 0}
            />
            <ActionButton
              label={`Decline ${request.orderNumber}`}
              onPress={() => act('decline')}
              pending={decide.isPending}
              disabled={note.trim().length === 0}
            />
          </View>
        </View>
      ) : null}
    </View>
  );
}

export function AdminReturnsView() {
  const [status, setStatus] = useState<ReturnStatus | 'all'>('Requested');
  const [page, setPage] = useState(1);
  const query = useReturnsQuery(status, page);

  return (
    <ScrollView contentContainerClassName="w-full max-w-content gap-lg self-center p-lg">
      <ScreenTitle title="Returns" />
      <View className="flex-row flex-wrap gap-sm">
        {FILTERS.map((filter) => (
          <Pressable
            key={filter}
            accessibilityRole="button"
            accessibilityState={{ selected: filter === status }}
            onPress={() => {
              setStatus(filter);
              setPage(1);
            }}
            className={`min-h-touch min-w-touch justify-center rounded-pill border px-md ${filter === status ? 'border-primary bg-primary-subtle' : 'border-border bg-surface'}`}
          >
            <Text className={`text-body ${filter === status ? 'font-semibold text-primary-text' : 'text-foreground'}`}>
              {filter === 'all' ? 'All' : filter}
            </Text>
          </Pressable>
        ))}
      </View>
      <AdminPanel>
        {query.data ? (
          query.data.items.length === 0 ? (
            <Text className="text-body text-muted">No return requests.</Text>
          ) : (
            <View>
              {query.data.items.map((request) => (
                <ReturnCard key={request.id} request={request} />
              ))}
            </View>
          )
        ) : query.isError ? (
          <ErrorState message={userMessage(query.error)} onRetry={() => void query.refetch()} />
        ) : (
          <LoadingState />
        )}
      </AdminPanel>
      {query.data && query.data.items.length > 0 && query.data.totalPages > 1 ? (
        <View className="flex-row flex-wrap items-center gap-md">
          <ActionButton label="Previous page" onPress={() => setPage(page - 1)} disabled={page <= 1} />
          <Text className="text-body text-foreground">{`Page ${query.data.page} of ${query.data.totalPages}`}</Text>
          <ActionButton label="Next page" onPress={() => setPage(page + 1)} disabled={page >= query.data.totalPages} />
        </View>
      ) : null}
    </ScrollView>
  );
}
