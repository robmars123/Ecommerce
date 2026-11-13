/** "Good morning, Ada" from the device's local hour (app context 4.14). The name is omitted when blank. */
export function greeting(now: Date, displayName?: string): string {
  const hour = now.getHours();
  const part = hour >= 5 && hour < 12 ? 'Good morning' : hour >= 12 && hour < 18 ? 'Good afternoon' : 'Good evening';
  const name = displayName?.trim();
  return name ? `${part}, ${name}` : part;
}
