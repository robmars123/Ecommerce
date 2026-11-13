import { narrowBreakpoint, sidebarBreakpoint } from '@/lib/design-tokens';

import { orderDetailLayout } from './layout';

describe('orderDetailLayout', () => {
  it.each([
    [narrowBreakpoint - 1, { twoColumn: false, compact: true }],
    [narrowBreakpoint, { twoColumn: false, compact: false }],
    [sidebarBreakpoint - 1, { twoColumn: false, compact: false }],
    [sidebarBreakpoint, { twoColumn: true, compact: false }],
  ])('L1: at %i px', (width, expected) => {
    expect(orderDetailLayout(width)).toEqual(expected);
  });
});
