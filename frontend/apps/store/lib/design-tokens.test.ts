import { colors, sidebarBreakpoint, themes, width } from '@/lib/design-tokens';

const HEX = /^#[0-9a-f]{6}([0-9a-f]{2})?$/i;

const LEGACY_KEYS = [
  'background',
  'surface',
  'surface-raised',
  'border',
  'foreground',
  'muted',
  'primary',
  'primary-foreground',
  'primary-subtle',
  'primary-text',
  'selected',
  'selected-foreground',
  'success',
  'warning',
  'danger',
];

describe('design tokens', () => {
  it('T1: dark and light define the same keys, all valid hex colours', () => {
    expect(Object.keys(themes.light).sort()).toEqual(Object.keys(themes.dark).sort());
    for (const theme of [themes.dark, themes.light]) {
      for (const value of Object.values(theme)) {
        expect(typeof value).toBe('string');
        expect(value).toMatch(HEX);
      }
    }
  });

  it('T2: colors is the dark theme and keeps every key the app used before', () => {
    expect(colors).toBe(themes.dark);
    for (const key of LEGACY_KEYS) {
      expect(themes.dark).toHaveProperty(key);
    }
  });

  it('T3: sidebar breakpoint is positive and the sidebar fits the narrowest phone', () => {
    expect(sidebarBreakpoint).toBeGreaterThan(0);
    expect(width.sidebar).toMatch(/^\d+px$/);
    expect(parseInt(width.sidebar, 10)).toBeLessThan(320);
  });
});
