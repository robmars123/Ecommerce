import { getApiBaseUrl } from './config';
import { newCorrelationId } from './correlation-id';

export type ApiErrorKind = 'network' | 'timeout' | 'not-found' | 'validation' | 'client' | 'server' | 'config';

export const REQUEST_TIMEOUT_MS = 15_000;

export class ApiError extends Error {
  kind: ApiErrorKind;
  status?: number;
  title?: string;
  correlationId?: string;
  fieldErrors?: Record<string, string[]>;

  constructor(
    kind: ApiErrorKind,
    message: string,
    details: { status?: number; title?: string; correlationId?: string; fieldErrors?: Record<string, string[]> } = {},
  ) {
    super(message);
    Object.setPrototypeOf(this, new.target.prototype);
    this.name = 'ApiError';
    this.kind = kind;
    this.status = details.status;
    this.title = details.title;
    this.correlationId = details.correlationId;
    this.fieldErrors = details.fieldErrors;
  }
}

type ApiGetOptions = {
  params?: Record<string, string | number | undefined>;
  signal?: AbortSignal;
};

function buildUrl(base: string, path: string, params?: ApiGetOptions['params']): string {
  const query = Object.entries(params ?? {})
    .filter(([, value]) => value !== undefined && value !== '')
    .map(([key, value]) => `${encodeURIComponent(key)}=${encodeURIComponent(String(value))}`)
    .join('&');
  return `${base}${path}${query ? `?${query}` : ''}`;
}

function kindForStatus(status: number): ApiErrorKind {
  if (status === 404) return 'not-found';
  if (status === 400 || status === 422) return 'validation';
  if (status >= 500) return 'server';
  return 'client';
}

type ParsedBody = { parsed: true; value: unknown } | { parsed: false };

async function readJson(response: Response): Promise<ParsedBody> {
  try {
    const text = await response.text();
    return { parsed: true, value: JSON.parse(text) };
  } catch {
    return { parsed: false };
  }
}

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null ? (value as Record<string, unknown>) : {};
}

function toFieldErrors(value: unknown): Record<string, string[]> | undefined {
  const record = asRecord(value);
  const entries = Object.entries(record).filter(
    (entry): entry is [string, string[]] => Array.isArray(entry[1]) && entry[1].every((m) => typeof m === 'string'),
  );
  return entries.length > 0 ? Object.fromEntries(entries) : undefined;
}

export async function apiGet<T>(path: string, options: ApiGetOptions = {}): Promise<T> {
  const base = getApiBaseUrl();
  if (!base) {
    throw new ApiError('config', 'EXPO_PUBLIC_API_BASE_URL is not set.');
  }

  const controller = new AbortController();
  let timedOut = false;
  const timer = setTimeout(() => {
    timedOut = true;
    controller.abort();
  }, REQUEST_TIMEOUT_MS);
  const forwardAbort = () => controller.abort();
  options.signal?.addEventListener('abort', forwardAbort);

  try {
    let response: Response;
    try {
      response = await fetch(buildUrl(base, path, options.params), {
        method: 'GET',
        headers: { Accept: 'application/json', 'X-Correlation-ID': newCorrelationId() },
        signal: controller.signal,
      });
    } catch (error) {
      if (timedOut) {
        throw new ApiError('timeout', 'The request timed out.');
      }
      if (options.signal?.aborted) {
        throw error;
      }
      throw new ApiError('network', 'The request could not be completed.');
    }

    const result = await readJson(response);
    const body = asRecord(result.parsed ? result.value : undefined);
    const correlationId =
      (typeof body.correlationId === 'string' ? body.correlationId : undefined) ??
      response.headers.get('X-Correlation-ID') ??
      undefined;

    if (!response.ok) {
      throw new ApiError(kindForStatus(response.status), `Request failed with status ${response.status}.`, {
        status: response.status,
        title: typeof body.title === 'string' ? body.title : undefined,
        correlationId,
        fieldErrors: toFieldErrors(body.errors),
      });
    }

    if (!result.parsed) {
      throw new ApiError('server', 'The server sent an unreadable response.', { status: response.status, correlationId });
    }
    return result.value as T;
  } finally {
    clearTimeout(timer);
    options.signal?.removeEventListener('abort', forwardAbort);
  }
}

const MESSAGES: Record<ApiErrorKind, string> = {
  network: "Can't reach the store. Check your connection and try again.",
  timeout: 'The store is taking too long to respond. Please try again.',
  'not-found': "We couldn't find that.",
  validation: 'Something about that request was not right. Please change it and try again.',
  client: 'That request could not be completed. Please try again.',
  server: 'Something went wrong on our side. Please try again.',
  config: "The app isn't configured to reach the store.",
};

/** Plain-language copy for the UI. Never exposes internal messages. */
export function userMessage(error: unknown): string {
  return error instanceof ApiError ? MESSAGES[error.kind] : 'Something went wrong. Please try again.';
}
