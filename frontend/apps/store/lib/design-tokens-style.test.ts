import { narrowBreakpoint, sidebarBreakpoint, themes, webFontFamily } from '@/lib/design-tokens';

describe('design tokens (admin style)', () => {
  it('T4: the colour keys the admin style consumes exist in both modes', () => {
    for (const key of ['subtle', 'warning-subtle', 'warning-border', 'danger-foreground', 'danger-subtle', 'danger-border']) {
      expect(themes.dark).toHaveProperty(key);
      expect(themes.light).toHaveProperty(key);
    }
  });

  it('T4: the narrow breakpoint sits below the sidebar breakpoint', () => {
    expect(narrowBreakpoint).toBeGreaterThan(0);
    expect(narrowBreakpoint).toBeLessThan(sidebarBreakpoint);
  });

  it('F1: web font stacks are Inter and JetBrains Mono over system fallbacks', () => {
    expect(webFontFamily.sans).toBe('Inter, system-ui, -apple-system, "Segoe UI", Roboto, sans-serif');
    expect(webFontFamily.mono).toBe('"JetBrains Mono", monospace');
  });
});
