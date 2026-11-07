// Type-only check (tsc, no runtime): each platform file must match what callers see in platform.d.ts. Adding an export
// to one half only, or changing a signature, fails `tsc --noEmit`.
type Shared = typeof import('./platform');

export const nativeConforms: Shared = {} as typeof import('./platform.native');
export const webConforms: Shared = {} as typeof import('./platform.web');
