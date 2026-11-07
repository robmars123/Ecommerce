import { getApiBaseUrl } from './config';
import { newCorrelationId } from './correlation-id';

export type ApiErrorKind =
  | 'network'
  | 'timeout'
  | 'not-found'
  | 'validation'
  | 'unauthorized'
  | 'forbidden'
  | 'conflict'
  | 'client'
  | 'server'
  | 'config';

export const REQUEST_TIMEOUT_MS = 15_000;

/** One line of a 409 `checkout-lines-rejected` reply (arch 9 checkout contract). */
export type RejectedLine = { variantId: string; reason: 'OutOfStock' | 'PriceChanged' | 'Unavailable'; available?: number };

/** Holds no headers, no request and no token: an error can be logged or rendered safely. */
export class ApiError extends Error {
  kind: ApiErrorKind;
  status?: number;
  title?: string;
  correlationId?: string;
  fieldErrors?: Record<string, string[]>;
  /** From a 409 stock reply: how many are available. */
  available?: number;
  /** The problem `type` the server sets as a machine-readable cause, such as `reservation-expired`. */
  problemType?: string;
  /** From a 409 `checkout-lines-rejected` reply: each line that cannot be ordered, and why. */
  lines?: RejectedLine[];
  /** From a 503 `payment-provider-unavailable` reply: the order that exists and is paid by repeating the request. */
  orderId?: string;

  constructor(
    kind: ApiErrorKind,
    message: string,
    details: {
      status?: number;
      title?: string;
      correlationId?: string;
      fieldErrors?: Record<string, string[]>;
      available?: number;
      problemType?: string;
      lines?: RejectedLine[];
      orderId?: string;
    } = {},
  ) {
    super(message);
    Object.setPrototypeOf(this, new.target.prototype);
    this.name = 'ApiError';
    this.kind = kind;
    this.status = details.status;
    this.title = details.title;
    this.correlationId = details.correlationId;
    this.fieldErrors = details.fieldErrors;
    this.available = details.available;
    this.problemType = details.problemType;
    this.lines = details.lines;
    this.orderId = details.orderId;
  }
}

type ApiGetOptions = {
  params?: Record<string, string | number | undefined>;
  signal?: AbortSignal;
  /** 'bearer' (default) sends the access token when there is one; 'none' never does. */
  auth?: 'bearer' | 'none';
};

type ApiSendOptions = {
  body?: unknown;
  signal?: AbortSignal;
  auth?: 'bearer' | 'none';
  /** Only 'cookie' changes the request: it sends X-Auth-Transport: cookie and includes credentials. */
  refreshTransport?: 'cookie' | 'body';
  /** Extra request headers, such as Idempotency-Key. Never an Authorization header: the session owns that. */
  headers?: Record<string, string>;
};

/**
 * The session registers these (the lib never imports features). refresh() is single-flight on the session side and
 * resolves true when a new access token is available.
 */
type AuthHandlers = {
  getAccessToken: () => string | undefined;
  refresh: () => Promise<boolean>;
};

let authHandlers: AuthHandlers | undefined;

export function setAuthHandlers(handlers: AuthHandlers | undefined): void {
  authHandlers = handlers;
}

function buildUrl(base: string, path: string, params?: ApiGetOptions['params']): string {
  const query = Object.entries(params ?? {})
    .filter(([, value]) => value !== undefined && value !== '')
    .map(([key, value]) => `${encodeURIComponent(key)}=${encodeURIComponent(String(value))}`)
    .join('&');
  return `${base}${path}${query ? `?${query}` : ''}`;
}

function kindForStatus(status: number): ApiErrorKind {
  if (status === 401) return 'unauthorized';
  if (status === 403) return 'forbidden';
  if (status === 404) return 'not-found';
  if (status === 409) return 'conflict';
  if (status === 400 || status === 422) return 'validation';
  if (status >= 500) return 'server';
  return 'client';
}

type ParsedBody = { parsed: true; value: unknown } | { parsed: false };

async function readJson(response: Response): Promise<ParsedBody> {
  try {
    const text = await response.text();
    // 202 and 204 carry no body: an empty body is an empty object.
    return { parsed: true, value: text.length === 0 ? {} : JSON.parse(text) };
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

function toRejectedLines(value: unknown): RejectedLine[] | undefined {
  if (!Array.isArray(value)) {
    return undefined;
  }
  const lines = value.filter(
    (line): line is RejectedLine => typeof line?.variantId === 'string' && typeof line?.reason === 'string',
  );
  return lines.length > 0 ? lines : undefined;
}

type RequestSpec = {
  method: 'GET' | 'POST' | 'PUT' | 'DELETE';
  path: string;
  params?: ApiGetOptions['params'];
  body?: unknown;
  signal?: AbortSignal;
  auth: 'bearer' | 'none';
  refreshTransport?: 'cookie' | 'body';
  headers?: Record<string, string>;
};

/** One HTTP attempt. Returns the parsed body or throws an ApiError. */
async function attempt<T>(spec: RequestSpec, token: string | undefined): Promise<T> {
  const base = getApiBaseUrl();
  if (!base) {
    throw new ApiError('config', 'EXPO_PUBLIC_API_BASE_URL is not set.');
  }
  // Already cancelled: do not start a request at all.
  if (spec.signal?.aborted) {
    throw spec.signal.reason ?? new Error('Aborted');
  }

  const controller = new AbortController();
  let timedOut = false;
  const timer = setTimeout(() => {
    timedOut = true;
    controller.abort();
  }, REQUEST_TIMEOUT_MS);
  const forwardAbort = () => controller.abort();
  spec.signal?.addEventListener('abort', forwardAbort);

  try {
    const sentId = newCorrelationId();
    const headers: Record<string, string> = { ...spec.headers, Accept: 'application/json', 'X-Correlation-ID': sentId };
    if (spec.body !== undefined) {
      headers['Content-Type'] = 'application/json';
    }
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }
    if (spec.refreshTransport === 'cookie') {
      headers['X-Auth-Transport'] = 'cookie';
    }

    let response: Response;
    try {
      response = await fetch(buildUrl(base, spec.path, spec.params), {
        method: spec.method,
        headers,
        body: spec.body === undefined ? undefined : JSON.stringify(spec.body),
        credentials: spec.refreshTransport === 'cookie' ? 'include' : undefined,
        signal: controller.signal,
      });
    } catch (error) {
      if (timedOut) {
        throw new ApiError('timeout', 'The request timed out.', { correlationId: sentId });
      }
      if (spec.signal?.aborted) {
        throw error;
      }
      throw new ApiError('network', 'The request could not be completed.', { correlationId: sentId });
    }

    const result = await readJson(response);
    // The timer can fire while the body is still being read.
    if (timedOut) {
      throw new ApiError('timeout', 'The request timed out.', { correlationId: sentId });
    }
    const body = asRecord(result.parsed ? result.value : undefined);
    const correlationId =
      (typeof body.correlationId === 'string' ? body.correlationId : undefined) ??
      response.headers.get('X-Correlation-ID') ??
      sentId;

    if (!response.ok) {
      throw new ApiError(kindForStatus(response.status), `Request failed with status ${response.status}.`, {
        status: response.status,
        title: typeof body.title === 'string' ? body.title : undefined,
        correlationId,
        fieldErrors: toFieldErrors(body.errors),
        available: typeof body.available === 'number' ? body.available : undefined,
        problemType: typeof body.type === 'string' ? body.type : undefined,
        lines: toRejectedLines(body.lines),
        orderId: typeof body.orderId === 'string' ? body.orderId : undefined,
      });
    }

    if (!result.parsed) {
      throw new ApiError('server', 'The server sent an unreadable response.', { status: response.status, correlationId });
    }
    return result.value as T;
  } finally {
    clearTimeout(timer);
    spec.signal?.removeEventListener('abort', forwardAbort);
  }
}

/**
 * A request that carried a token and got 401 refreshes once and retries once. A second 401, or a failed refresh,
 * is "unauthorized". The refresh is single-flight in the session, so parallel 401s share one refresh.
 */
async function execute<T>(spec: RequestSpec): Promise<T> {
  const token = spec.auth === 'bearer' ? authHandlers?.getAccessToken() : undefined;
  try {
    return await attempt<T>(spec, token);
  } catch (error) {
    if (!(error instanceof ApiError) || error.kind !== 'unauthorized' || !token || !authHandlers) {
      throw error;
    }
    if (!(await authHandlers.refresh())) {
      throw error;
    }
    return attempt<T>(spec, authHandlers.getAccessToken());
  }
}

export function apiGet<T>(path: string, options: ApiGetOptions = {}): Promise<T> {
  return execute<T>({
    method: 'GET',
    path,
    params: options.params,
    signal: options.signal,
    auth: options.auth ?? 'bearer',
  });
}

export function apiSend<T>(method: 'POST' | 'PUT' | 'DELETE', path: string, options: ApiSendOptions = {}): Promise<T> {
  return execute<T>({
    method,
    path,
    body: options.body,
    signal: options.signal,
    auth: options.auth ?? 'bearer',
    refreshTransport: options.refreshTransport,
    headers: options.headers,
  });
}

const MESSAGES: Record<ApiErrorKind, string> = {
  network: "Can't reach the store. Check your connection and try again.",
  timeout: 'The store is taking too long to respond. Please try again.',
  'not-found': "We couldn't find that.",
  validation: 'Something about that request was not right. Please change it and try again.',
  unauthorized: 'Please log in to continue.',
  forbidden: "You don't have access to that.",
  conflict: 'That changed while you were editing. Reload and try again.',
  client: 'That request could not be completed. Please try again.',
  server: 'Something went wrong on our side. Please try again.',
  config: "The app isn't configured to reach the store.",
};

/** Plain-language copy for the UI. Never exposes internal messages. */
export function userMessage(error: unknown): string {
  return error instanceof ApiError ? MESSAGES[error.kind] : 'Something went wrong. Please try again.';
}
