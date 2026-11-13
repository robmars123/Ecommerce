import { useState } from 'react';
import { Text } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { ErrorState } from '@/components/ErrorState';
import { Panel } from '@/components/Panel';
import { TextField } from '@/components/TextField';
import { ApiError, userMessage } from '@/lib/api-client';

import { useReturnRequestMutation } from '../hooks/useReturnRequestMutation';
import type { OrderDetail, ReturnItemBody } from '../types';

const MAX_REASON = 500;

function messageFor(error: unknown): string {
  if (error instanceof ApiError && error.kind === 'conflict') {
    return error.problemType === 'return-already-open'
      ? 'You already have an open return request for this order.'
      : 'Only a delivered order can be returned.';
  }
  return userMessage(error);
}

/**
 * Ask to return part of a delivered order (F8). Shown for a Delivered order only. There is no optimistic "requested" state:
 * the confirmation appears after the server created the request, and a failure shows plain text with a retry.
 */
export function ReturnRequestForm({ order }: { order: OrderDetail }) {
  const mutation = useReturnRequestMutation(order.id);
  const [quantities, setQuantities] = useState<Record<string, string>>({});
  const [reason, setReason] = useState('');
  const [problem, setProblem] = useState<string | null>(null);

  if (order.status !== 'Delivered') {
    return null;
  }
  if (mutation.isSuccess) {
    return (
      <Panel title="Return items">
        <Text accessibilityRole="alert" className="text-body text-foreground">
          Return requested. We will email you when it has been reviewed.
        </Text>
      </Panel>
    );
  }

  function submit() {
    const items: ReturnItemBody[] = order.items
      .map((item) => ({ variantId: item.variantId, quantity: Number.parseInt(quantities[item.variantId] ?? '', 10) }))
      .filter((item) => Number.isInteger(item.quantity) && item.quantity > 0);
    if (items.length === 0) {
      setProblem('Enter how many of at least one item you want to return.');
      return;
    }
    if (reason.trim().length === 0 || reason.trim().length > MAX_REASON) {
      setProblem(`Tell us why, in 1 to ${MAX_REASON} characters.`);
      return;
    }
    setProblem(null);
    mutation.mutate({ items, reason: reason.trim() });
  }

  if (mutation.isError) {
    return (
      <Panel title="Return items">
        <ErrorState message={messageFor(mutation.error)} onRetry={submit} />
      </Panel>
    );
  }

  return (
    <Panel title="Return items">
      {order.items.map((item) => (
        <TextField
          key={item.variantId}
          label={`Return how many of ${item.name} (up to ${item.quantity})`}
          value={quantities[item.variantId] ?? ''}
          onChangeText={(text) => setQuantities((current) => ({ ...current, [item.variantId]: text }))}
          keyboardType="number-pad"
        />
      ))}
      <TextField label="Why are you returning it" value={reason} onChangeText={setReason} multiline />
      {problem ? (
        <Text accessibilityRole="alert" className="text-body text-danger">
          {problem}
        </Text>
      ) : null}
      <ActionButton label="Request a return" onPress={submit} pending={mutation.isPending} />
    </Panel>
  );
}
