import { randomUUID } from 'expo-crypto';

/** One new key per logical request (arch 8.1, 8.2). Keep it and reuse it when retrying the same request. */
export function newIdempotencyKey(): string {
  return randomUUID();
}
