// Global Jest mocks for native modules that have no JS implementation (Slice 3).
jest.mock('@react-native-async-storage/async-storage', () => require('@react-native-async-storage/async-storage/jest/async-storage-mock'));

// expo-crypto has native code only; the tests need real, distinct UUIDs.
jest.mock('expo-crypto', () => ({ randomUUID: () => require('node:crypto').randomUUID() }));

// The payment SDK has native code only and throws on import without it (Slice 4). Tests of the native payment form replace this mock.
jest.mock('@stripe/stripe-react-native', () => ({
  StripeProvider: ({ children }) => children,
  useStripe: () => ({ initPaymentSheet: jest.fn(), presentPaymentSheet: jest.fn() }),
}));

// The live status socket opens a real connection (Slice 6). Screen tests never do: lib/realtime.test.ts and the live-status
// tests replace this mock with their own.
jest.mock('@/lib/realtime', () => ({ subscribeToOrderStatus: jest.fn(() => () => undefined) }));
