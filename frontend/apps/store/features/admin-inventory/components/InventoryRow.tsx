import { useState } from 'react';
import { Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { TextField } from '@/components/TextField';
import type { StockStatus } from '@/features/catalog/types';
import { ApiError, userMessage } from '@/lib/api-client';

import { buildAdjustRequest, conflictMessage, validateAdjust } from '../adjust-model';
import { useAdjustStockMutation } from '../hooks/useAdjustStockMutation';
import type { AdminInventoryItem } from '../types';

type InventoryRowProps = {
  item: AdminInventoryItem;
  open: boolean;
  onOpen: () => void;
  onClose: () => void;
  /** Rereads the current page only; wired to that page's query refetch. */
  onReload: () => void;
};

const STATUS: Record<StockStatus, { text: string; className: string }> = {
  InStock: { text: 'In stock', className: 'text-foreground' },
  Low: { text: 'Low stock', className: 'text-warning font-semibold' },
  Out: { text: 'Out of stock', className: 'text-danger font-semibold' },
  Unknown: { text: 'Unknown', className: 'text-muted' },
};

type RowError = { message: string; reload: boolean; delta?: string; reason?: string };

function describeError(error: unknown, expectedOnHand: number | undefined): RowError {
  if (error instanceof ApiError) {
    if (error.status === 409 && expectedOnHand !== undefined) {
      const { message, stale } = conflictMessage(error, expectedOnHand);
      return { message, reload: stale };
    }
    if (error.status === 400 && (error.fieldErrors?.delta || error.fieldErrors?.reason)) {
      return { message: '', reload: false, delta: error.fieldErrors.delta?.[0], reason: error.fieldErrors.reason?.[0] };
    }
    if (error.status === 404) {
      return { message: 'This variant no longer exists. Reload the list.', reload: true };
    }
  }
  return { message: userMessage(error), reload: false };
}

/** Mounted only while the row is open, so every open starts with empty fields and no leftover error. */
function AdjustForm({ item, onClose, onReload }: { item: AdminInventoryItem; onClose: () => void; onReload: () => void }) {
  const adjust = useAdjustStockMutation();
  const [quantity, setQuantity] = useState('');
  const [reason, setReason] = useState('');
  const [invalid, setInvalid] = useState<{ quantity?: string; reason?: string }>({});

  const failure = adjust.error ? describeError(adjust.error, adjust.variables?.expectedOnHand) : null;

  function submit(direction: 'add' | 'remove') {
    const errors = validateAdjust(quantity, reason);
    setInvalid(errors);
    if (errors.quantity || errors.reason) {
      return;
    }
    // expectedOnHand is read from the row as rendered now, never cached at open time.
    adjust.mutateAsync(buildAdjustRequest(item, quantity, reason, direction)).then(onClose, () => undefined);
  }

  function reload() {
    onReload();
    adjust.reset();
  }

  return (
    <View className="gap-md">
      <TextField
        label={`Quantity for ${item.sku}`}
        value={quantity}
        onChangeText={setQuantity}
        keyboardType="number-pad"
        error={invalid.quantity ?? failure?.delta}
      />
      <TextField label={`Reason for ${item.sku}`} value={reason} onChangeText={setReason} error={invalid.reason ?? failure?.reason} />
      {failure && failure.message ? (
        <Text accessibilityRole="alert" className="text-body text-danger">
          {failure.message}
        </Text>
      ) : null}
      {failure?.reload ? <ActionButton label="Reload" onPress={reload} /> : null}
      <View className="flex-row flex-wrap gap-md">
        <ActionButton label="Add stock" onPress={() => submit('add')} pending={adjust.isPending} />
        <ActionButton label="Remove stock" onPress={() => submit('remove')} pending={adjust.isPending} />
        <ActionButton label="Cancel" onPress={onClose} />
      </View>
    </View>
  );
}

export function InventoryRow({ item, open, onOpen, onClose, onReload }: InventoryRowProps) {
  const status = STATUS[item.stockStatus];

  return (
    <View className="gap-sm border-b border-border py-md">
      <Text className="text-body font-semibold text-foreground">{`${item.productName} · ${item.variantName}`}</Text>
      <Text className="text-small text-muted">{`SKU ${item.sku}`}</Text>
      <Text className="text-body text-foreground">{`On hand ${item.onHand} · Reserved ${item.reserved} · Available ${item.available}`}</Text>
      <View className="flex-row flex-wrap gap-md">
        <Text className={`text-small ${status.className}`}>{status.text}</Text>
        {item.isActive ? null : <Text className="text-small text-muted">Inactive</Text>}
      </View>

      {open ? (
        <AdjustForm item={item} onClose={onClose} onReload={onReload} />
      ) : (
        <View className="flex-row">
          <ActionButton label={`Adjust ${item.sku}`} onPress={onOpen} />
        </View>
      )}
    </View>
  );
}
