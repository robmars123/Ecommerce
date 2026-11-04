/** A new 32-hex-character id for the X-Correlation-ID header. Not security-sensitive, so Math.random is fine. */
export function newCorrelationId(): string {
  let id = '';
  for (let i = 0; i < 32; i++) {
    id += Math.floor(Math.random() * 16).toString(16);
  }
  return id;
}
