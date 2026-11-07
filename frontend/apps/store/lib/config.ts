/**
 * Base URL of the API (EXPO_PUBLIC_API_BASE_URL). There is deliberately no fallback: a missing value is a
 * visible configuration error, never a silent call to a guessed host. The property access must stay static
 * so Expo can inline it at build time.
 */
export function getApiBaseUrl(): string | undefined {
  const raw = process.env.EXPO_PUBLIC_API_BASE_URL?.trim();
  if (!raw) {
    return undefined;
  }
  return raw.replace(/\/+$/, '');
}

/**
 * Publishable key of the payment provider (EXPO_PUBLIC_STRIPE_PUBLISHABLE_KEY). Public by design, but still no fallback:
 * a missing value is a visible configuration error in the payment step, never a guessed key. Static access, like above.
 */
export function getStripePublishableKey(): string | undefined {
  const raw = process.env.EXPO_PUBLIC_STRIPE_PUBLISHABLE_KEY?.trim();
  return raw ? raw : undefined;
}
