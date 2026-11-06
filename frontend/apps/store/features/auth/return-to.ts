/**
 * Where to go after login. Only a same-app path is accepted: one leading slash, no "//", no backslash, no control
 * character. Anything else (an open-redirect attempt, an empty value) becomes "/". An array takes its first value.
 */
export function safeReturnTo(value: string | string[] | undefined): string {
  const raw = Array.isArray(value) ? value[0] : value;
  if (typeof raw !== 'string' || raw.length === 0) return '/';
  if (!raw.startsWith('/') || raw.startsWith('//')) return '/';
  for (const char of raw) {
    const code = char.charCodeAt(0);
    if (char === '\\' || code < 0x20 || code === 0x7f) return '/';
  }
  return raw;
}
