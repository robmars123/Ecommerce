// react-dom ships no types here and @types/react-dom is not a dependency; the web platform test renders with this much of it.
declare module 'react-dom/client' {
  import type { ReactNode } from 'react';

  export type Root = { render(children: ReactNode): void; unmount(): void };
  export function createRoot(container: Element): Root;
}
