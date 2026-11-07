import { ApiError, type ApiErrorKind } from './api-client';

export type ClientLogEntry = {
  /** Static, e.g. 'products.list' — never params. */
  source: string;
  kind: ApiErrorKind | 'unexpected';
  status?: number;
  correlationId?: string;
  /** Only for non-ApiError errors; redacted, at most 200 characters. */
  message?: string;
};

const MAX_MESSAGE = 200;
const MAX_CORRELATION_ID = 64;

/** Removes anything that looks like a credential, an email or a card-length number. */
export function redact(text: string): string {
  return text
    .replace(/Bearer\s+[A-Za-z0-9._~+/=-]+/gi, 'Bearer [redacted]')
    .replace(/\beyJ[\w-]+\.[\w-]+\.[\w-]+/g, '[token]')
    .replace(/[^\s@]+@[^\s@]+\.[^\s@]+/g, '[email]')
    .replace(/\b\d{13,19}\b/g, '[number]');
}

/** One console call per failure. ApiError content other than kind, status and correlation id is never logged. */
export function logClientError(error: unknown, source: string): void {
  const safeSource = redact(source);
  if (error instanceof ApiError) {
    const entry: ClientLogEntry = {
      source: safeSource,
      kind: error.kind,
      status: error.status,
      correlationId: error.correlationId === undefined ? undefined : redact(error.correlationId).slice(0, MAX_CORRELATION_ID),
    };
    const loud = error.kind === 'server' || error.kind === 'network' || error.kind === 'timeout';
    console[loud ? 'error' : 'warn']('[client-error]', entry);
    return;
  }
  const entry: ClientLogEntry = {
    source: safeSource,
    kind: 'unexpected',
    message: error instanceof Error ? redact(error.message).slice(0, MAX_MESSAGE) : undefined,
  };
  console.error('[client-error]', entry);
}
