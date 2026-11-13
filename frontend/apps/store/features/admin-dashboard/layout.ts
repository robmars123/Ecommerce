import { narrowBreakpoint, sidebarBreakpoint } from '@/lib/design-tokens';

export type DashboardLayout = {
  kpiBasis: 'basis-full' | 'basis-[45%]' | 'basis-[22%]';
  panelsSideBySide: boolean;
  narrow: boolean;
  wide: boolean;
};

/** Width-driven layout of the dashboard: KPI columns (4 / 2 / 1) and whether the two-panel rows sit side by side. */
export function dashboardLayout(width: number): DashboardLayout {
  const wide = width >= sidebarBreakpoint;
  const narrow = width < narrowBreakpoint;
  return { kpiBasis: wide ? 'basis-[22%]' : narrow ? 'basis-full' : 'basis-[45%]', panelsSideBySide: wide, narrow, wide };
}
