import { useState } from 'react';
import { Pressable, Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
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
    <View className="gap-sm rounded-control border border-border p-md">
      <Text className="text-body font-semibold text-foreground">{`${request.orderNumber} · ${request.status}`}</Text>
      <Text className="text-small text-muted">{formatOrderDate(request.createdAt)}</Text>
      {request.items.map((item) => (
        <Text key={item.variantId} className="text-body text-foreground">{`${item.name} × ${item.quantity}`}</Text>
      ))}
      <Text className="text-body text-foreground">{`Reason: ${request.reason}`}</Text>
      {request.adminNote ? <Text className="text-small text-muted">{`Note: ${request.adminNote}`}</Text> : null}
      {open ? (
        <View className="gap-sm">
          <TextField label={`Decision note for ${request.orderNumber}`} value={note} onChangeText={setNote} />
          {decide.error ? (
            <Text accessibilityRole="alert" className="text-body text-danger">
              {userMessage(decide.error)}
            </Text>
          ) : null}
          <View className="flex-row gap-sm">
            <ActionButton label={`Approve ${request.orderNumber}`} onPress={() => act('approve')} pending={decide.isPending} disabled={note.trim().length === 0} />
            <ActionButton label={`Decline ${request.orderNumber}`} onPress={() => act('decline')} pending={decide.isPending} disabled={note.trim().length === 0} />
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
    <View className="w-full max-w-content flex-1 gap-md self-center p-lg">
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
            className={`min-h-touch justify-center rounded-control border px-md ${filter === status ? 'border-primary' : 'border-border'}`}
          >
            <Text className="text-body text-foreground">{filter === 'all' ? 'All' : filter}</Text>
          </Pressable>
        ))}
      </View>
      {query.data ? (
        query.data.items.length === 0 ? (
          <Text className="text-body text-muted">No return requests.</Text>
        ) : (
          <View className="gap-md">
            {query.data.items.map((request) => (
              <ReturnCard key={request.id} request={request} />
            ))}
            {query.data.totalPages > 1 ? (
              <View className="flex-row items-center gap-md">
                <ActionButton label="Previous page" onPress={() => setPage(page - 1)} disabled={page <= 1} />
                <Text className="text-body text-foreground">{`Page ${query.data.page} of ${query.data.totalPages}`}</Text>
                <ActionButton label="Next page" onPress={() => setPage(page + 1)} disabled={page >= query.data.totalPages} />
              </View>
            ) : null}
          </View>
        )
      ) : query.isError ? (
        <ErrorState message={userMessage(query.error)} onRetry={() => void query.refetch()} />
      ) : (
        <LoadingState />
      )}
    </View>
  );
}
