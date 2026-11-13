import { Linking, Pressable, Text } from 'react-native';

type TrackingLinkProps = {
  trackingNumber: string;
  trackingUrl: string | null;
  label?: string;
};

/** Only an https URL becomes a link; anything else shows the plain tracking number. */
export function TrackingLink({ trackingNumber, trackingUrl, label = 'Track shipment' }: TrackingLinkProps) {
  if (trackingUrl === null || !trackingUrl.startsWith('https://')) {
    return <Text className="text-body text-foreground">{trackingNumber}</Text>;
  }
  return (
    <Pressable
      accessibilityRole="link"
      accessibilityLabel={label}
      onPress={() => void Linking.openURL(trackingUrl)}
      className="min-h-touch justify-center"
    >
      <Text className="text-ui font-semibold text-primary-text underline">{label}</Text>
    </Pressable>
  );
}
