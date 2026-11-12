import { useState } from 'react';
import { Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { TextField } from '@/components/TextField';
import { useSession } from '@/features/auth/hooks/useSession';
import { ApiError, userMessage } from '@/lib/api-client';

import { actionsFor, validateReason, validateShip, type OrderActionKind } from '../order-actions';
import { useOrderActionMutation } from '../hooks/hooks';
import type { AdminOrderDetail } from '../types';

const LABELS: Record<OrderActionKind, string> = {
  process: 'Process',
  ship: 'Ship',
  deliver: 'Deliver',
  'override-processing': 'Override to Processing',
  'override-delivered': 'Override to Delivered',
};

/** The server's own words for a 409 (its title) or a 400 (the first field message); anything else uses the generic copy. */
function messageFor(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.kind === 'conflict' && error.title) {
      return error.title;
    }
    const first = error.fieldErrors ? Object.values(error.fieldErrors)[0]?.[0] : undefined;
    if (error.kind === 'validation' && first) {
      return first;
    }
  }
  return userMessage(error);
}

/**
 * Fulfillment buttons (11.5). Buttons follow the server status, but the server is the guard: Admin only (rule 11), and a
 * repeat or illegal move answers 409. No optimistic status: after the call settles the order is refetched.
 */
export function OrderActions({ detail }: { detail: AdminOrderDetail }) {
  const session = useSession();
  const action = useOrderActionMutation(detail.order.id);
  const [open, setOpen] = useState<OrderActionKind | null>(null);
  const [carrier, setCarrier] = useState('');
  const [tracking, setTracking] = useState('');
  const [reason, setReason] = useState('');
  const [problems, setProblems] = useState<{ carrier?: string; tracking?: string; reason?: string }>({});

  const kinds = actionsFor(detail.order.status);
  if (session.user?.role !== 'Admin' || kinds.length === 0) {
    return null;
  }

  function choose(kind: OrderActionKind) {
    action.reset();
    setProblems({});
    if (kind === 'process') {
      setOpen(null);
      action.mutate({ kind: 'process' });
    } else if (kind === 'deliver') {
      setOpen(null);
      action.mutate({ kind: 'deliver' });
    } else {
      setOpen(kind);
    }
  }

  function close() {
    setOpen(null);
    setCarrier('');
    setTracking('');
    setReason('');
    setProblems({});
  }

  function confirm() {
    if (open === 'ship') {
      const found = validateShip(carrier, tracking);
      setProblems({ carrier: found.carrier, tracking: found.tracking });
      if (!found.carrier && !found.tracking) {
        action.mutate({ kind: 'ship', carrierCode: carrier.trim(), trackingNumber: tracking.trim() }, { onSuccess: close });
      }
    } else if (open === 'override-processing' || open === 'override-delivered') {
      const found = validateReason(reason);
      setProblems({ reason: found });
      if (!found) {
        action.mutate(
          { kind: 'override', toStatus: open === 'override-processing' ? 'Processing' : 'Delivered', reason: reason.trim() },
          { onSuccess: close },
        );
      }
    }
  }

  return (
    <View className="gap-md rounded-card border border-border bg-surface p-lg">
      <Text accessibilityRole="header" className="text-title font-semibold text-foreground">
        Fulfillment
      </Text>
      <View className="flex-row flex-wrap gap-sm">
        {kinds.map((kind) => (
          <ActionButton key={kind} label={LABELS[kind]} onPress={() => choose(kind)} pending={action.isPending} />
        ))}
      </View>
      {open === 'ship' ? (
        <View className="gap-sm">
          <TextField label="Carrier code" value={carrier} onChangeText={setCarrier} error={problems.carrier} autoCapitalize="none" />
          <TextField label="Tracking number" value={tracking} onChangeText={setTracking} error={problems.tracking} autoCapitalize="none" />
          <ActionButton label="Confirm ship" onPress={confirm} pending={action.isPending} />
        </View>
      ) : null}
      {open === 'override-processing' || open === 'override-delivered' ? (
        <View className="gap-sm">
          <TextField label="Override reason" value={reason} onChangeText={setReason} error={problems.reason} multiline />
          <ActionButton label="Confirm override" onPress={confirm} pending={action.isPending} />
        </View>
      ) : null}
      {action.error ? (
        <Text accessibilityRole="alert" className="text-body text-danger">
          {messageFor(action.error)}
        </Text>
      ) : null}
    </View>
  );
}
