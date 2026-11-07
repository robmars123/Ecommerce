import { newIdempotencyKey } from './idempotency-key';

describe('newIdempotencyKey', () => {
  it('returns distinct UUID-shaped keys', () => {
    const keys = new Set(Array.from({ length: 20 }, () => newIdempotencyKey()));

    expect(keys.size).toBe(20);
    keys.forEach((key) => expect(key).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/));
  });
});
