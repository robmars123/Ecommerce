// Single source of design tokens (arch §5.5). CommonJS so tailwind.config.js can require it.
// Sizes are px on purpose: NativeWind's native rem is 14px, px is exact on every platform.
const touch = 44;

const dark = {
  background: '#0f1210',
  surface: '#171b18',
  'surface-raised': '#1e231f',
  border: '#2a302b',
  foreground: '#eef1ee',
  muted: '#9ba39d',
  primary: '#3dae6f',
  'primary-foreground': '#f5fbf7',
  'primary-subtle': '#1c3326',
  'primary-text': '#4cc283',
  selected: '#e8ece9',
  'selected-foreground': '#111412',
  success: '#3dae6f',
  warning: '#e0a43a',
  danger: '#e35d4f',
  scrim: '#000000b3',
};

const light = {
  background: '#f6f8f6',
  surface: '#ffffff',
  'surface-raised': '#eef2ef',
  border: '#d6ddd8',
  foreground: '#111412',
  muted: '#5a645d',
  primary: '#23804c',
  'primary-foreground': '#ffffff',
  'primary-subtle': '#e2f2e8',
  'primary-text': '#1d6e41',
  selected: '#111412',
  'selected-foreground': '#f5fbf7',
  success: '#23804c',
  warning: '#9a6410',
  danger: '#c0392b',
  scrim: '#11141266',
};

module.exports = {
  // Numeric px for the rare JS-prop cases (the arch 5.5 Tests fallback); utilities use the px strings below.
  touch,
  // Tailwind colour utilities compile to these CSS variables; ThemeRoot sets them from `themes[mode]` (arch 5.5).
  colorVariables: Object.fromEntries(Object.keys(dark).map((key) => [key, `var(--color-${key})`])),
  // Both modes share one key set.
  themes: { dark, light },
  sidebarBreakpoint: 768,
  spacing: {
    xs: '4px',
    sm: '8px',
    md: '12px',
    lg: '16px',
    xl: '24px',
    '2xl': '32px',
    '3xl': '48px',
  },
  fontSize: {
    caption: '12px',
    small: '14px',
    body: '16px',
    title: '18px',
    heading: '24px',
    display: '32px',
  },
  borderRadius: {
    control: '8px',
    input: '10px',
    card: '12px',
    pill: '9999px',
  },
  minHeight: { touch: `${touch}px`, textarea: '96px' },
  minWidth: { touch: `${touch}px` },
  maxWidth: { content: '1200px' },
  width: { thumb: '72px', sidebar: '240px' },
  height: { thumb: '72px' },
};
