import { Link } from 'expo-router';
import { useState } from 'react';
import { Text, View } from 'react-native';

import { ActionButton } from '@/components/ActionButton';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import type { Money } from '@/features/catalog/types';
import { useOrderQuery } from '@/features/orders/hooks/useOrderQuery';
import { ApiError, userMessage } from '@/lib/api-client';
import { formatMoney } from '@/lib/money';
import { PaymentForm, type PaymentFormResult } from '@/lib/platform';

import { useRetryPaymentMutation } from '../hooks/useRetryPaymentMutation';

type PaymentStepProps = {
  orderId: string;
  /** The secret from the checkout reply. Null after a page refresh: the customer continues with a new attempt. */
  initialSecret: string | null;
  /** The server total from the checkout reply (F6); the order read is used when this screen was reached by URL. */
  placedTotal?: Money;
  /** The payment provider reported success (a redirect returned): start by waiting for the server. */
  startConfirming?: boolean;
  onCheckoutAgain: () => void;
};

/**
 * Pay for one placed order. The payment form's result is a hint only (rule 5): after it the screen polls the order every
 * 2 seconds and says "paid" only when the server does. A declined card offers a new attempt on the same order.
 */
export function PaymentStep({ orderId, initialSecret, placedTotal, startConfirming = false, onCheckoutAgain }: PaymentStepProps) {
  const [secret, setSecret] = useState<string | null>(initialSecret);
  const [confirming, setConfirming] = useState(startConfirming);
  const [formKey, setFormKey] = useState(0);
  const [declined, setDeclined] = useState<string | null>(null);
  const order = useOrderQuery(orderId, confirming);
  const retry = useRetryPaymentMutation(orderId);

  const total = placedTotal ?? order.data?.grandTotal;
  const status = order.data?.status;
  const expired = retry.error instanceof ApiError && retry.error.problemType === 'reservation-expired';

  function onResult(result: PaymentFormResult) {
    if (result.status === 'succeeded' || result.status === 'processing') {
      setConfirming(true);
      return;
    }
    setDeclined(result.status === 'canceled' ? 'The payment was canceled.' : (result.message ?? 'The payment did not go through.'));
  }

  function newAttempt() {
    retry.retry((clientSecret) => {
      setSecret(clientSecret);
      setDeclined(null);
      setFormKey((key) => key + 1);
    });
  }

  if (order.isError && !order.data) {
    return <ErrorState message={userMessage(order.error)} onRetry={() => void order.refetch()} />;
  }
  if (status === undefined) {
    return <LoadingState />;
  }
  if (status === 'Cancelled') {
    return (
      <View className="gap-md">
        <Text accessibilityRole="alert" className="text-body text-danger">
          This order was cancelled because the payment was not completed in time. If a payment went through, it is refunded automatically.
        </Text>
        <ActionButton label="Check out again" onPress={onCheckoutAgain} />
      </View>
    );
  }
  if (status !== 'PendingPayment') {
    return (
      <View className="gap-md">
        <Text accessibilityRole="header" className="text-title font-semibold text-foreground">
          Payment received
        </Text>
        <Text className="text-body text-foreground">Thank you. Your order is confirmed.</Text>
        <Link href={`/orders/${orderId}`} asChild>
          <Text accessibilityRole="link" className="min-h-touch py-md text-body font-semibold text-foreground">
            View your order
          </Text>
        </Link>
      </View>
    );
  }

  return (
    <View className="gap-md">
      {total ? <Text className="text-title font-semibold text-foreground">{`Total ${formatMoney(total)}`}</Text> : null}
      {confirming ? (
        <View className="gap-sm">
          <Text accessibilityRole="header" className="text-title font-semibold text-foreground">
            Awaiting payment confirmation
          </Text>
          <Text className="text-body text-foreground">We are confirming your payment. This usually takes a few seconds.</Text>
          <LoadingState />
        </View>
      ) : (
        <>
          {declined ? (
            <Text accessibilityRole="alert" className="text-body text-danger">
              {declined}
            </Text>
          ) : null}
          {expired ? (
            <View className="gap-md">
              <Text accessibilityRole="alert" className="text-body text-danger">
                Your reservation has expired.
              </Text>
              <ActionButton label="Check out again" onPress={onCheckoutAgain} />
            </View>
          ) : secret && !declined ? (
            <PaymentForm
              key={formKey}
              clientSecret={secret}
              returnPath={`/checkout?orderId=${encodeURIComponent(orderId)}`}
              onResult={onResult}
            />
          ) : (
            <View className="gap-md">
              {retry.error ? (
                <Text accessibilityRole="alert" className="text-body text-danger">
                  {userMessage(retry.error)}
                </Text>
              ) : null}
              <ActionButton label={declined ? 'Retry payment' : 'Continue to payment'} onPress={newAttempt} pending={retry.isPending} />
            </View>
          )}
        </>
      )}
    </View>
  );
}
