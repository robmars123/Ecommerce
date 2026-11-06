import { ApiError, setAuthHandlers } from '@/lib/api-client';
import { refreshTokenStorage, refreshTokenTransport } from '@/lib/platform';

import { refresh as refreshApi } from './api';
import type { SessionUser, TokenResponse } from './types';

export type SessionStatus = 'restoring' | 'signed-out' | 'signed-in';
export type SessionSnapshot = { status: SessionStatus; user: SessionUser | null };

// The access token lives in this module variable only. It is never part of the snapshot, so it can never reach
// React state, a render, a log line or a query cache.
let accessToken: string | undefined;
let snapshot: SessionSnapshot = { status: 'restoring', user: null };
let inFlightRefresh: Promise<boolean> | undefined;
let onSignedOut: (() => void) | undefined;
const listeners = new Set<() => void>();

function publish(next: SessionSnapshot) {
  snapshot = next;
  listeners.forEach((listener) => listener());
}

export function getSessionSnapshot(): SessionSnapshot {
  return snapshot;
}

export function subscribeToSession(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function getAccessToken(): string | undefined {
  return accessToken;
}

/** Called after the session ends for any reason (logout, a refresh the server rejected). */
export function setOnSignedOut(callback: (() => void) | undefined): void {
  onSignedOut = callback;
}

/** Saves a token response. Native stores the refresh token; on web it is only ever in the httpOnly cookie. */
export async function startSession(tokens: TokenResponse): Promise<void> {
  accessToken = tokens.accessToken;
  if (refreshTokenTransport() === 'body' && tokens.refreshToken) {
    await refreshTokenStorage.set(tokens.refreshToken);
  }
  publish({ status: 'signed-in', user: tokens.user });
}

export async function endSession(): Promise<void> {
  accessToken = undefined;
  await refreshTokenStorage.clear();
  const wasSignedIn = snapshot.status !== 'signed-out';
  publish({ status: 'signed-out', user: null });
  if (wasSignedIn) onSignedOut?.();
}

async function runRefresh(): Promise<boolean> {
  try {
    const stored = refreshTokenTransport() === 'body' ? await refreshTokenStorage.get() : null;
    if (refreshTokenTransport() === 'body' && !stored) {
      await endSession();
      return false;
    }
    await startSession(await refreshApi(stored));
    return true;
  } catch (error) {
    if (error instanceof ApiError && error.kind === 'unauthorized') {
      await endSession();
    }
    // Network trouble or a server error: keep the session, the caller can try again later.
    return false;
  }
}

/** Single-flight: concurrent callers share one request, so a rotating refresh token is never used twice. */
export function refreshSession(): Promise<boolean> {
  inFlightRefresh ??= runRefresh().finally(() => {
    inFlightRefresh = undefined;
  });
  return inFlightRefresh;
}

/** Once at app start. Native with no stored token is signed out without any request. */
export async function restoreSession(): Promise<void> {
  // Already decided (a sign-in or an earlier restore): nothing to restore.
  if (snapshot.status !== 'restoring') {
    return;
  }
  if (refreshTokenTransport() === 'body' && !(await refreshTokenStorage.get())) {
    publish({ status: 'signed-out', user: null });
    return;
  }
  const restored = await refreshSession();
  if (!restored && snapshot.status === 'restoring') {
    publish({ status: 'signed-out', user: null });
  }
}

/** Test support: back to the initial state without touching storage or the network. */
export function resetSession(): void {
  accessToken = undefined;
  inFlightRefresh = undefined;
  onSignedOut = undefined;
  publish({ status: 'restoring', user: null });
}

setAuthHandlers({ getAccessToken, refresh: refreshSession });
