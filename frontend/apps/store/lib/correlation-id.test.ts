import { newCorrelationId } from './correlation-id';

describe('newCorrelationId', () => {
  it('is 32 lowercase hex characters and matches the server accepted pattern', () => {
    const id = newCorrelationId();
    expect(id).toMatch(/^[a-f0-9]{32}$/);
    expect(id).toMatch(/^[A-Za-z0-9._:-]{1,64}$/);
  });

  it('differs between calls', () => {
    const ids = new Set(Array.from({ length: 50 }, () => newCorrelationId()));
    expect(ids.size).toBe(50);
  });
});
