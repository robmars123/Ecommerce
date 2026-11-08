import { useState } from 'react';
import { Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { TextField } from '@/components/TextField';
import { ApiError, userMessage } from '@/lib/api-client';
import { formatMoney } from '@/lib/money';

import { useRefundMutation } from '../hooks/hooks';
import type { AdminOrderDetail } from '../types';

const AMOUNT_PATTERN = /^\d+(\.\d{1,2})?$/;
const MAX_REASON = 500;

/** The server's reason for a 409, in plain language. Anything else uses the generic copy. */
function messageFor(error: unknown): string {
  if (error instanceof ApiError && error.kind === 'conflict') {
    switch (error.problemType) {
      case 'refund-ceiling-exceeded':
        return 'That is more than is left to refund. Lower the amount.';
      case 'refund-state-not-allowed':
        return 'This order cannot be refunded that way in its current state.';
      case 'no-refundable-payment':
        return 'This order has no captured payment to refund.';
      default:
        return 'That refund conflicts with an earlier one. Reload the order and check.';
    }
  }
  return userMessage(error);
}

/**
 * Refund form (F13). The amount is sent as a decimal string, never a number. One Idempotency-Key per attempt, reused if the
 * same attempt is retried. There is no optimistic status: after the server answers, the order is refetched and the screen
 * shows its Pending and Remaining amounts.
 */
export function RefundForm({ detail }: { detail: AdminOrderDetail }) {
  const refund = useRefundMutation(detail.order.id);
  const [amount, setAmount] = useState('');
  const [reason, setReason] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const currency = detail.order.grandTotal.currency;

  function submit() {
    const trimmed = amount.trim();
    if (!AMOUNT_PATTERN.test(trimmed) || Number(trimmed) <= 0) {
      setProblem('Enter an amount greater than zero, such as 12.50.');
      return;
    }
    if (reason.trim().length === 0 || reason.trim().length > MAX_REASON) {
      setProblem(`Enter a reason of 1 to ${MAX_REASON} characters.`);
      return;
    }
    setProblem(null);
    refund.submit({ amount: { amount: trimmed, currency }, reason: reason.trim() });
  }

  return (
    <View className="gap-md">
      <Text accessibilityRole="header" className="text-title font-semibold text-foreground">
        Refund
      </Text>
      <Text className="text-body text-foreground">{`Remaining to refund: ${formatMoney(detail.remaining)}`}</Text>
      <TextField label={`Refund amount (${currency})`} value={amount} onChangeText={setAmount} keyboardType="decimal-pad" />
      <TextField label="Refund reason" value={reason} onChangeText={setReason} multiline />
      {problem ? (
        <Text accessibilityRole="alert" className="text-body text-danger">
          {problem}
        </Text>
      ) : null}
      {refund.error ? (
        <Text accessibilityRole="alert" className="text-body text-danger">
          {messageFor(refund.error)}
        </Text>
      ) : null}
      {refund.data ? (
        <Text accessibilityRole="alert" className="text-body text-foreground">
          {`Refund of ${formatMoney(refund.data.amount)} requested. It stays Pending until the payment provider confirms it.`}
        </Text>
      ) : null}
      <ActionButton label="Refund" onPress={submit} pending={refund.isPending} />
    </View>
  );
}
