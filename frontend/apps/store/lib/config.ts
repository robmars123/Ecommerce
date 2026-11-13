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

/**
 * The URL an image can be loaded from. The API returns its own images as a path (`/api/v1/images/...`) when no image base URL is
 * configured, so a leading `/` gets the API base in front; an absolute URL (a CDN) passes through. A relative path with no API
 * base is null, so the placeholder shows rather than a broken request to a guessed host.
 */
export function resolveImageUrl(url: string | null | undefined): string | null {
  if (!url) {
    return null;
  }
  if (url.startsWith('/')) {
    const base = getApiBaseUrl();
    return base ? `${base}${url}` : null;
  }
  return url;
}
