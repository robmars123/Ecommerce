import { ApiError, type ApiErrorKind } from './api-client';
import { logClientError, redact } from './logger';

const jwt = 'eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2lnbmF0dXJl';

describe('redact', () => {
  it('strips bearer tokens, JWTs, emails and long digit runs', () => {
    expect(redact('Authorization: Bearer abc.def-123')).toBe('Authorization: Bearer [redacted]');
    expect(redact(`token ${jwt} end`)).toBe('token [token] end');
    expect(redact('mail user@x.com now')).toBe('mail [email] now');
    expect(redact('card 4242424242424242 x')).toBe('card [number] x');
  });

  it('leaves a 32-hex correlation id intact', () => {
    const id = '0123456789abcdef0123456789abcdef';
    expect(redact(id)).toBe(id);
  });
});

describe('logClientError', () => {
  let errorSpy: jest.SpyInstance;
  let warnSpy: jest.SpyInstance;

  beforeEach(() => {
    errorSpy = jest.spyOn(console, 'error').mockImplementation(() => undefined);
    warnSpy = jest.spyOn(console, 'warn').mockImplementation(() => undefined);
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('logs a server ApiError once with only source, kind, status and correlation id', () => {
    const error = new ApiError('server', 'Request failed with status 500.', {
      status: 500,
      title: 'user@x.com failed',
      correlationId: 'corr-1',
      fieldErrors: { email: ['user@x.com is taken'] },
    });

    logClientError(error, 'products.list');

    expect(errorSpy).toHaveBeenCalledTimes(1);
    expect(warnSpy).not.toHaveBeenCalled();
    expect(errorSpy).toHaveBeenCalledWith('[client-error]', {
      source: 'products.list',
      kind: 'server',
      status: 500,
      correlationId: 'corr-1',
    });
    const serialized = JSON.stringify(errorSpy.mock.calls[0]);
    expect(serialized).not.toContain('title');
    expect(serialized).not.toContain('user@x.com');
  });

  it.each<[ApiErrorKind, number]>([
    ['not-found', 404],
    ['validation', 400],
    ['unauthorized', 401],
  ])('logs %s as a warning, not an error', (kind, status) => {
    logClientError(new ApiError(kind, 'm', { status }), 'products.detail');

    expect(warnSpy).toHaveBeenCalledTimes(1);
    expect(errorSpy).not.toHaveBeenCalled();
  });

  it('logs a non-ApiError as unexpected with a redacted message capped at 200 chars', () => {
    logClientError(new Error(`boom for user@x.com ${'x'.repeat(300)}`), 'mutation');

    expect(errorSpy).toHaveBeenCalledTimes(1);
    const entry = errorSpy.mock.calls[0][1] as { kind: string; message: string };
    expect(entry.kind).toBe('unexpected');
    expect(entry.message).not.toContain('user@x.com');
    expect(entry.message.length).toBeLessThanOrEqual(200);
  });
});
