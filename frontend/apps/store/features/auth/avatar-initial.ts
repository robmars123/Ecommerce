import type { SessionUser } from './types';

/** The name shown for a user: the display name, or the email when that is blank (same rule as the admin sidebar footer). */
export function shownName(user: Pick<SessionUser, 'displayName' | 'email'>): string {
  return user.displayName.trim() || user.email;
}

/** First code point of the shown name, upper-cased ("?" when there is none). Array.from keeps an astral character whole. */
export function avatarInitial(user: Pick<SessionUser, 'displayName' | 'email'>): string {
  const first = Array.from(shownName(user).trim())[0];
  return first ? first.toUpperCase() : '?';
}
