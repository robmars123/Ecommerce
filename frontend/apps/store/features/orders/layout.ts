import { narrowBreakpoint, sidebarBreakpoint } from '@/lib/design-tokens';

export type OrderDetailLayout = { twoColumn: boolean; compact: boolean };

/** Width-driven layout of the order detail: items and timeline beside totals and shipping from the sidebar breakpoint, tighter below the narrow one. */
export function orderDetailLayout(width: number): OrderDetailLayout {
  return { twoColumn: width >= sidebarBreakpoint, compact: width < narrowBreakpoint };
}
