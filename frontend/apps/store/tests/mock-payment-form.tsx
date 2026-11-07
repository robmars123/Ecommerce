import { Pressable, Text, View } from 'react-native';

import type { PaymentFormProps } from '@/lib/platform';

/** Every PaymentForm render of the test, newest last. The screen under test hands it a secret and listens for its result. */
export const renderedPaymentForms: PaymentFormProps[] = [];

function Button({ label, onPress }: { label: string; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={label} onPress={onPress}>
      <Text>{label}</Text>
    </Pressable>
  );
}

/** Stands in for the provider payment UI (needs the SDK and a real card): three buttons report a result. */
export function MockPaymentForm(props: PaymentFormProps) {
  renderedPaymentForms.push(props);
  return (
    <View>
      <Text>{`Payment form ${props.clientSecret}`}</Text>
      <Button label="Mock pay" onPress={() => props.onResult({ status: 'succeeded' })} />
      <Button label="Mock processing" onPress={() => props.onResult({ status: 'processing' })} />
      <Button label="Mock decline" onPress={() => props.onResult({ status: 'failed', message: 'Your card was declined.' })} />
    </View>
  );
}
