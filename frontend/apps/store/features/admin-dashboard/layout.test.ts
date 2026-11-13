import { narrowBreakpoint, sidebarBreakpoint } from '@/lib/design-tokens';

import { dashboardLayout } from './layout';

describe('dashboardLayout', () => {
  it.each([1280, sidebarBreakpoint])('L1: at %i px the KPIs take four columns and the panels sit side by side', (width) => {
    expect(dashboardLayout(width)).toEqual({ kpiBasis: 'basis-[22%]', panelsSideBySide: true, narrow: false, wide: true });
  });

  it.each([sidebarBreakpoint - 1, 900, narrowBreakpoint])('L2: at %i px the KPIs take two columns and the panels stack', (width) => {
    expect(dashboardLayout(width)).toEqual({ kpiBasis: 'basis-[45%]', panelsSideBySide: false, narrow: false, wide: false });
  });

  it.each([narrowBreakpoint - 1, 375])('L3: at %i px the KPIs take one column and the title shrinks', (width) => {
    expect(dashboardLayout(width)).toEqual({ kpiBasis: 'basis-full', panelsSideBySide: false, narrow: true, wide: false });
  });
});
