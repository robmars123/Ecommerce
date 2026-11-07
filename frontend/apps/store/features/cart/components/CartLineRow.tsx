import { Pressable, Text, View } from 'react-native';

import { formatMoney } from '@/lib/money';

import { ISSUE_TEXT } from '../cart-messages';
import type { CartLine, GuestLine } from '../types';

function StepButton({ label, symbol, onPress, disabled }: { label: string; symbol: string; onPress: () => void; disabled?: boolean }) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled: Boolean(disabled) }}
      disabled={disabled}
      onPress={onPress}
      className={`min-h-touch min-w-touch items-center justify-center rounded-control border border-border ${disabled ? 'opacity-50' : ''}`}
    >
      <Text className="text-body font-semibold text-foreground">{symbol}</Text>
    </Pressable>
  );
}

type RowActions = { onSetQuantity: (quantity: number) => void; onRemove: () => void; busy?: boolean };

function Controls({ name, quantity, onSetQuantity, onRemove, busy }: RowActions & { name: string; quantity: number }) {
  return (
    <View className="flex-row flex-wrap items-center gap-md">
      <StepButton
        label={`Decrease quantity of ${name}`}
        symbol="-"
        onPress={() => onSetQuantity(quantity - 1)}
        disabled={busy || quantity <= 1}
      />
      <Text accessibilityLabel={`Quantity of ${name}`} className="min-w-touch text-center text-body text-foreground">
        {quantity}
      </Text>
      <StepButton label={`Increase quantity of ${name}`} symbol="+" onPress={() => onSetQuantity(quantity + 1)} disabled={busy} />
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={`Remove ${name}`}
        disabled={busy}
        onPress={onRemove}
        className="min-h-touch min-w-touch justify-center px-md"
      >
        <Text className="text-body text-foreground">Remove</Text>
      </Pressable>
    </View>
  );
}

/** A server cart line. Every price and total is the server string, shown as given. */
export function CartLineRow({ line, ...actions }: { line: CartLine } & RowActions) {
  const name = `${line.productName} - ${line.variantName}`;
  return (
    <View className="gap-sm rounded-card border border-border bg-surface p-md">
      <Text className="text-body font-semibold text-foreground">{line.productName}</Text>
      <Text className="text-small text-muted">{line.variantName}</Text>
      <Text className="text-body text-foreground">{`${formatMoney(line.unitPrice)} each`}</Text>
      <Controls name={name} quantity={line.quantity} {...actions} />
      <Text className="text-body text-foreground">{`Line total ${formatMoney(line.lineTotal)}`}</Text>
      {line.issues.map((issue) => (
        <Text key={issue} accessibilityRole="alert" className="text-small text-danger">
          {ISSUE_TEXT[issue]}
        </Text>
      ))}
    </View>
  );
}

/** A device-cart line: the snapshot price is an estimate until sign-in, and no total is shown. */
export function GuestLineRow({ line, ...actions }: { line: GuestLine } & RowActions) {
  const name = `${line.snapshot.productName} - ${line.snapshot.variantName}`;
  return (
    <View className="gap-sm rounded-card border border-border bg-surface p-md">
      <Text className="text-body font-semibold text-foreground">{line.snapshot.productName}</Text>
      <Text className="text-small text-muted">{line.snapshot.variantName}</Text>
      <Text className="text-body text-foreground">{`Estimated price ${formatMoney(line.snapshot.price)} each`}</Text>
      <Controls name={name} quantity={line.quantity} {...actions} />
    </View>
  );
}
