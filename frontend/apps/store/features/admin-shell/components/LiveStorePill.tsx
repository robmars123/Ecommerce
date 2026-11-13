import { useEffect, useState } from 'react';
import { Text, View } from 'react-native';

import { subscribeToConnectionState } from '@/lib/realtime';

/** "Live store": whether the realtime socket is connected right now. A hint, never a health check; text says it, not just colour. */
export function LiveStorePill() {
  const [connected, setConnected] = useState(false);
  useEffect(() => subscribeToConnectionState(setConnected), []);
  return (
    <View
      accessible
      accessibilityLabel={connected ? 'Live store connected' : 'Live store offline'}
      className={`flex-row items-center gap-[6px] rounded-pill border px-md py-[5px] ${
        connected ? 'border-primary bg-primary-subtle' : 'border-border bg-surface'
      }`}
    >
      <View className={`h-[7px] w-[7px] rounded-pill ${connected ? 'bg-primary' : 'bg-subtle'}`} />
      <Text className={`text-meta font-semibold ${connected ? 'text-primary-text' : 'text-muted'}`}>
        {connected ? 'Live store' : 'Live store · offline'}
      </Text>
    </View>
  );
}
