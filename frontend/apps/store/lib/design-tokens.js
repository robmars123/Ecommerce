// Single source of design tokens (arch §5.5). CommonJS so tailwind.config.js can require it.
// Sizes are px on purpose: NativeWind's native rem is 14px, px is exact on every platform.
const touch = 44;

const dark = {
  background: '#0b0e13',
  surface: '#12161f',
  'surface-raised': '#0f131a',
  border: '#222836',
  foreground: '#f1f5f9',
  muted: '#94a3b8',
  subtle: '#64748b',
  primary: '#10b981',
  'primary-foreground': '#ffffff',
  'primary-subtle': '#0f2e25',
  'primary-text': '#10b981',
  selected: '#ffffff',
  'selected-foreground': '#0f172a',
  success: '#10b981',
  warning: '#f59e0b',
  'warning-subtle': '#2a2110',
  'warning-border': '#443612',
  danger: '#f87171',
  'danger-subtle': '#2b1414',
  'danger-border': '#4a1f1f',
  'danger-foreground': '#ffffff',
  scrim: '#000000b3',
};

const light = {
  background: '#f6f7f9',
  surface: '#ffffff',
  'surface-raised': '#eef0f3',
  border: '#e3e6eb',
  foreground: '#0f172a',
  muted: '#475569',
  subtle: '#94a3b8',
  primary: '#0a7a5c',
  'primary-foreground': '#ffffff',
  'primary-subtle': '#e6f4ef',
  'primary-text': '#0a7a5c',
  selected: '#0f172a',
  'selected-foreground': '#ffffff',
  success: '#0a7a5c',
  warning: '#b45309',
  'warning-subtle': '#fef3c7',
  'warning-border': '#fde68a',
  danger: '#b91c1c',
  'danger-subtle': '#fef2f2',
  'danger-border': '#fecaca',
  'danger-foreground': '#ffffff',
  scrim: '#11141266',
};

// Web only: app/+html.tsx loads the Google Fonts and applies these. Android and iOS never get a family, so they keep the system font.
const webFontFamily = {
  sans: 'Inter, system-ui, -apple-system, "Segoe UI", Roboto, sans-serif',
  mono: '"JetBrains Mono", monospace',
};

// A type role carries px line-height and letter-spacing (em and unitless ratios are not valid on native): size x ratio, 2 decimals.
const role = (size, lineHeight, letterSpacing) => [
  `${size}px`,
  {
    ...(lineHeight === undefined ? {} : { lineHeight: `${Math.round(size * lineHeight * 100) / 100}px` }),
    ...(letterSpacing === undefined ? {} : { letterSpacing: `${Math.round(size * letterSpacing * 100) / 100}px` }),
  },
];

module.exports = {
  // Numeric px for the rare JS-prop cases (the arch 5.5 Tests fallback); utilities use the px strings below.
  touch,
  // Tailwind colour utilities compile to these CSS variables; ThemeRoot sets them from `themes[mode]` (arch 5.5).
  colorVariables: Object.fromEntries(Object.keys(dark).map((key) => [key, `var(--color-${key})`])),
  // Both modes share one key set.
  themes: { dark, light },
  // The sidebar shows from 981 px (hidden at 980 and below); below 561 px the layout narrows further.
  sidebarBreakpoint: 981,
  narrowBreakpoint: 561,
  webFontFamily,
  spacing: {
    xs: '4px',
    sm: '8px',
    md: '12px',
    lg: '16px',
    xl: '24px',
    '2xl': '32px',
    '3xl': '48px',
    gutter: '14px',
    card: '18px',
    page: '28px',
  },
  fontSize: {
    caption: '12px',
    small: '14px',
    body: '16px',
    title: '18px',
    heading: '24px',
    display: '32px',
    'page-title': role(26, 1.05, -0.03),
    'page-title-sm': role(23, 1.05, -0.03),
    kpi: role(28, 1, -0.04),
    'panel-title': role(14.5, undefined, -0.01),
    ui: role(13.5, 1.5),
    'ui-sm': role(13, 1.5),
    meta: role(12.5, 1.5),
    label: role(12, undefined, 0.06),
    'nav-label': role(11, undefined, 0.08),
    eyebrow: role(11, undefined, 0.02),
    badge: role(11, undefined, 0.04),
  },
  borderRadius: {
    control: '8px',
    input: '10px',
    card: '12px',
    panel: '14px',
    nav: '9px',
    pill: '9999px',
  },
  minHeight: { touch: `${touch}px`, textarea: '96px' },
  minWidth: { touch: `${touch}px` },
  maxWidth: { content: '1200px' },
  width: { thumb: '72px', sidebar: '232px' },
  height: { thumb: '72px' },
  // Shadows apply on web only (`web:shadow-*`); dark mode reuses the light card shadow.
  boxShadow: {
    card: '0 1px 2px rgba(15,23,42,.05), 0 8px 24px rgba(15,23,42,.04)',
    accent: '0 4px 14px rgba(16,185,129,.25)',
  },
};
