# Admin Dashboard CSS Values

Exact CSS values from the rebuilt admin dashboard — design tokens, typography, layout, and every component, in light and dark mode. Values are copied from the dashboard's stylesheet; hardcoded and inline values that do not come from a token are listed separately in section 9.

## 1. Design tokens

Defined on `:root`. Dark values apply under `@media (prefers-color-scheme: dark)`.

| Token | Light | Dark |
|---|---|---|
| `--bg` | `#f6f7f9` | `#0b0e13` |
| `--bg-2` | `#eef0f3` | `#0f131a` |
| `--card` | `#ffffff` | `#12161f` |
| `--card-2` | `#fcfcfd` | `#161b26` |
| `--border` | `#e3e6eb` | `#222836` |
| `--border-strong` | `#d4d8e0` | `#2e3648` |
| `--text` | `#0f172a` | `#f1f5f9` |
| `--text-2` | `#475569` | `#94a3b8` |
| `--text-3` | `#94a3b8` | `#64748b` |
| `--accent` | `#0a7a5c` | `#10b981` |
| `--accent-bg` | `#e6f4ef` | `#0f2e25` |
| `--accent-hover` | `#096c52` | `#34d399` |
| `--amber` | `#b45309` | `#f59e0b` |
| `--amber-bg` | `#fef3c7` | `#2a2110` |
| `--amber-border` | `#fde68a` | `#443612` |
| `--red` | `#b91c1c` | `#f87171` |
| `--red-bg` | `#fef2f2` | `#2b1414` |
| `--red-border` | `#fecaca` | `#4a1f1f` |
| `--blue` | `#1d4ed8` | `#60a5fa` |
| `--blue-bg` | `#eff6ff` | `#16233d` |
| `--shadow` | `0 1px 2px rgba(15,23,42,.05), 0 8px 24px rgba(15,23,42,.04)` | `0 1px 2px rgba(0,0,0,.4), 0 8px 24px rgba(0,0,0,.25)` |
| `--radius` | `14px` | `14px` (not redefined) |

Other root settings:

| Property | Value |
|---|---|
| `color-scheme` (meta) | `light dark` |
| Card / panel radius | `var(--radius)` = `14px` |
| Story card / stories hero radius | `16px` |
| Small radius (logo, KPI icon, thumb) | `8px` – `9px` |
| Pill radius | `999px` |

## 2. Base and typography

| Element / class | Values |
|---|---|
| `*` | `box-sizing: border-box` |
| `html, body` | `margin: 0; padding: 0; background: var(--bg); color: var(--text)` |
| Font stack | `Inter, system-ui, -apple-system, Segoe UI, Roboto, sans-serif` |
| Font smoothing | `-webkit-font-smoothing: antialiased` |
| Google Fonts loaded | `Inter:wght@400;500;600;700` and `JetBrains Mono:wght@500` |
| `.mono` | `font-family: "JetBrains Mono", monospace` |
| `a` | `color: inherit; text-decoration: none` |
| `button` | `font-family: inherit; cursor: pointer; min-height: 40px` |

Type scale used across the dashboard:

| Use | Size | Weight | Letter-spacing | Line-height |
|---|---:|---:|---:|---:|
| Page title `h1` | `26px` (`23px` ≤560px) | `700` | `-.03em` | `1.05` |
| Stories hero `h2` | `20px` | inherited | `-.03em` | `1.1` |
| KPI value | `28px` | `700` | `-.04em` | `1` |
| Revenue panel totals (inline) | `20px` | `700` | `-.03em` | — |
| Panel title `h3` | `14.5px` | inherited | `-.01em` | — |
| Story title `h3` | `15px` | inherited | `-.015em` | `1.25` |
| Body / nav / controls | `13px` – `13.5px` | `500` – `600` | — | `1.5` |
| Panel subtitle | `12.5px` | — | — | `1.4` |
| Meta line | `12.5px` | — | — | — |
| KPI label | `12px` | `600` | `.06em`, uppercase | — |
| Nav label | `11px` | `600` | `.08em`, uppercase | — |
| Brand eyebrow | `11px` | `500` | `.02em`, uppercase | — |
| Badge / pill | `11px` | `700` | `.04em` (badge) | — |

## 3. Layout, sidebar and topbar

| Element | Values |
|---|---|
| `.shell` | `display: flex; min-height: 100vh` |
| `.sidebar` | `width: 232px; flex-shrink: 0; background: var(--card); border-right: 1px solid var(--border); padding: 20px 14px; display: flex; flex-direction: column; gap: 6px; position: sticky; top: 0; height: 100vh` |
| `.main` | `flex: 1; min-width: 0; padding: 0 28px 36px` |
| `.topbar` | `height: 60px; display: flex; align-items: center; justify-content: space-between; border-bottom: 1px solid var(--border); margin: 0 -28px; padding: 0 28px; position: sticky; top: 0; z-index: 20` |
| `.topbar` background | `background: color-mix(in srgb, var(--bg) 88%, transparent); backdrop-filter: blur(8px)` |
| `.brand` | `display: flex; align-items: center; gap: 10px` |
| `.logo` | `width: 34px; height: 34px; border-radius: 9px; background: #0f172a; color: #fff; display: grid; place-items: center; font-weight: 700; font-size: 15px; letter-spacing: -.02em` |
| `.logo` (dark) | `background: #fff; color: #0f172a` |
| `.brand b` | `font-size: 14px; letter-spacing: -.02em; line-height: 1` |
| `.brand span` | `display: block; font-size: 11px; color: var(--text-3); font-weight: 500; margin-top: 2px; letter-spacing: .02em; text-transform: uppercase` |
| `.nav-label` | `font-size: 11px; letter-spacing: .08em; text-transform: uppercase; color: var(--text-3); font-weight: 600; padding: 14px 10px 6px` |
| `.nav-item` | `display: flex; align-items: center; gap: 10px; padding: 9px 10px; border-radius: 9px; font-size: 13.5px; font-weight: 500; color: var(--text-2); border: 1px solid transparent` |
| `.nav-item:hover` | `background: var(--bg)` |
| `.nav-item.active` | `background: var(--accent-bg); color: var(--text); border-color: var(--border)` |
| `.nav-item .dot` | `margin-left: auto; font-size: 11px; background: var(--red); color: #fff; min-width: 20px; height: 20px; border-radius: 999px; display: grid; place-items: center; padding: 0 6px; font-weight: 700` |
| `.nav-icon` | `width: 18px; text-align: center; font-size: 15px` |
| `.sidebar-footer` | `margin-top: auto; background: var(--bg); border: 1px solid var(--border); border-radius: 12px; padding: 12px` |
| `.sidebar-footer p` | `margin: 0; font-size: 12.5px; line-height: 1.5; color: var(--text-2)` |
| `.avatar-row` | `display: flex; align-items: center; gap: 10px; padding-top: 14px; border-top: 1px solid var(--border); margin-top: 14px` |
| `.avatar` | `width: 32px; height: 32px; border-radius: 999px; background: #e2e8f0; display: grid; place-items: center; font-weight: 700; font-size: 12px` |
| `.avatar-row b` | `font-size: 13px; display: block` |
| `.avatar-row span` | `font-size: 12px; color: var(--text-3)` |
| `.search` | `flex: 1; max-width: 340px; margin-left: 20px` |
| `.search input` | `width: 100%; height: 38px; border-radius: 999px; border: 1px solid var(--border); background: var(--card); padding: 0 14px 0 36px; font-size: 13.5px; color: var(--text)` plus the search SVG icon (stroke `#94a3b8`) at `background-position: 12px center; background-repeat: no-repeat` |

## 4. Page header, controls and tabs

| Element | Values |
|---|---|
| `.page-head` | `display: flex; flex-wrap: wrap; gap: 16px; align-items: flex-start; justify-content: space-between; padding: 22px 0 16px` |
| `.page-head h1` | `font-size: 26px; letter-spacing: -.03em; margin: 0; line-height: 1.05; font-weight: 700` |
| `.page-head p` | `margin: 6px 0 0; color: var(--text-2); font-size: 13.5px; line-height: 1.5; max-width: 620px` |
| `.controls` | `display: flex; flex-wrap: wrap; gap: 8px; align-items: center` |
| `.seg` | `display: flex; background: var(--card); border: 1px solid var(--border); border-radius: 999px; padding: 3px; box-shadow: var(--shadow)` |
| `.seg button` | `border: 0; background: transparent; padding: 0 14px; border-radius: 999px; font-size: 13px; font-weight: 600; color: var(--text-2); min-height: 36px` |
| `.seg button.active` | `background: #0f172a; color: #fff` — dark: `background: #fff; color: #0f172a` |
| `.btn-refresh` | `background: var(--accent); color: #fff; border: 0; border-radius: 999px; padding: 0 16px; font-weight: 600; font-size: 13.5px; display: inline-flex; align-items: center; gap: 7px; box-shadow: 0 4px 14px rgba(16,185,129,.25)` |
| `.btn-refresh:active` | `transform: scale(.98)` |
| `.meta-line` | `display: flex; flex-wrap: wrap; gap: 8px 14px; align-items: center; font-size: 12.5px; color: var(--text-3); margin-bottom: 16px` |
| `.live-dot` | `width: 7px; height: 7px; background: #10b981; border-radius: 999px; display: inline-block; box-shadow: 0 0 0 4px var(--accent-bg)` |
| `.view-tabs` | `display: flex; gap: 4px; border-bottom: 1px solid var(--border); margin-bottom: 20px; overflow: hidden` |
| `.view-tabs button` | `border: 0; background: transparent; padding: 11px 14px; font-size: 13.5px; font-weight: 600; color: var(--text-2); border-bottom: 2px solid transparent; margin-bottom: -1px; display: inline-flex; gap: 8px; align-items: center; min-height: 44px` |
| `.view-tabs button.active` | `color: var(--text); border-bottom-color: var(--accent)` |
| `.pill` | `font-size: 11px; font-weight: 700; padding: 2px 7px; border-radius: 999px; background: var(--bg-2); border: 1px solid var(--border); color: var(--text-2)` |
| `.pill.green` | `background: var(--accent-bg); color: var(--accent); border-color: transparent` |

## 5. KPI cards, alert banner and panels

| Element | Values |
|---|---|
| `.kpi-grid` | `display: grid; grid-template-columns: repeat(4,1fr); gap: 14px; margin-bottom: 14px` |
| `.kpi` | `background: var(--card); border: 1px solid var(--border); border-radius: var(--radius); padding: 18px; box-shadow: var(--shadow)` |
| `.kpi-top` | `display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px` |
| `.kpi label` | `font-size: 12px; letter-spacing: .06em; text-transform: uppercase; color: var(--text-3); font-weight: 600` |
| `.kpi-icon` | `width: 32px; height: 32px; border-radius: 8px; background: var(--bg); border: 1px solid var(--border); display: grid; place-items: center; font-size: 15px` |
| `.kpi-value` | `font-size: 28px; letter-spacing: -.04em; font-weight: 700; line-height: 1` |
| `.kpi-sub` | `margin-top: 8px; font-size: 12.5px; color: var(--text-2); line-height: 1.5` |
| `.kpi-sub b` | `color: var(--text)` |
| `.trend` | `font-size: 11px; font-weight: 700; padding: 3px 6px; border-radius: 999px; background: var(--bg); border: 1px solid var(--border)` |
| `.alert` | `display: flex; gap: 12px; align-items: flex-start; background: var(--amber-bg); border: 1px solid var(--amber-border); border-radius: 12px; padding: 13px 14px; margin-bottom: 14px` |
| `.alert strong` | `font-size: 13.5px` |
| `.alert p` | `margin: 2px 0 0; font-size: 13px; color: var(--text-2); line-height: 1.5` |
| `.alert a` | `font-weight: 600; text-decoration: underline; text-underline-offset: 2px` |
| `.grid-2` | `display: grid; grid-template-columns: 1.65fr .95fr; gap: 14px; margin-bottom: 14px` |
| `.grid-2b` | `display: grid; grid-template-columns: 1.15fr .85fr; gap: 14px` |
| `.panel` | `background: var(--card); border: 1px solid var(--border); border-radius: var(--radius); box-shadow: var(--shadow); overflow: hidden` |
| `.panel-head` | `display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; padding: 16px 18px 12px; border-bottom: 1px solid var(--border)` |
| `.panel-head h3` | `margin: 0; font-size: 14.5px; letter-spacing: -.01em` |
| `.panel-head p` | `margin: 3px 0 0; font-size: 12.5px; color: var(--text-3); line-height: 1.4` |
| `.panel-body` | `padding: 16px 18px 18px` |
| `.link` | `font-size: 12.5px; font-weight: 600; color: var(--accent); white-space: nowrap; padding: 6px 0` |

## 6. Revenue chart, orders donut and badges

| Element | Values |
|---|---|
| `.chart-wrap` | `padding: 6px 2px 0` |
| Revenue SVG | `viewBox: 0 0 560 170; width: 100%; height: 170` |
| Chart gridlines | `stroke: var(--border); stroke-dasharray: 3 5` at `y = 30, 75, 120` |
| Chart line | `stroke: #10b981; stroke-width: 2.5; stroke-linecap: round; fill: none`, flat at `y = 135` |
| Chart area fill | Linear gradient `#10b981` at `stop-opacity: .18` (0%) to `0` (100%), vertical |
| Chart end dot | `r: 4; fill: #10b981; stroke: white; stroke-width: 2` at `cx: 560, cy: 135` |
| Chart axis labels | `font-size: 11; fill: #94a3b8` |
| `.legend` | `display: flex; gap: 16px; flex-wrap: wrap; font-size: 12px; color: var(--text-2); margin-top: 10px` |
| `.legend i` | `width: 8px; height: 8px; border-radius: 999px; display: inline-block; margin-right: 6px` — dot colour inline: `background: #10b981` |
| `.donut-flex` | `display: flex; gap: 18px; align-items: center` |
| `.donut` | `width: 118px; height: 118px; flex-shrink: 0` — SVG `viewBox: 0 0 120 120` |
| Donut track | `cx: 60; cy: 60; r: 46; fill: none; stroke: var(--bg-2); stroke-width: 14` |
| Donut value arc | `stroke: #ef4444; stroke-width: 14; stroke-dasharray: 289 289; transform: rotate(-90 60 60); stroke-linecap: round` |
| Donut centre number | `font-size: 22; font-weight: 700; fill: var(--text)` at `x: 60, y: 58`, centred |
| Donut centre label | `font-size: 10; font-weight: 600; fill: #94a3b8` at `x: 60, y: 74`, centred |
| `.status-list` | `flex: 1; display: flex; flex-direction: column; gap: 10px` |
| `.status-row` | `display: flex; justify-content: space-between; align-items: center; font-size: 13px; padding: 8px 0; border-bottom: 1px dashed var(--border)` — last row: `border: 0` |
| `.badge` | `font-size: 11px; font-weight: 700; letter-spacing: .04em; text-transform: uppercase; padding: 4px 8px; border-radius: 999px` |
| `.badge-red` | `background: var(--red-bg); color: var(--red); border: 1px solid var(--red-border)` |
| `.badge-amber` | `background: var(--amber-bg); color: var(--amber); border: 1px solid var(--amber-border)` |
| `.badge-gray` | `background: var(--bg); color: var(--text-2); border: 1px solid var(--border)` |

## 7. Top products empty state and low stock

| Element | Values |
|---|---|
| `.empty` | `text-align: left; padding: 8px 0 2px` |
| `.empty-icon` | `width: 44px; height: 44px; border-radius: 12px; background: var(--bg); border: 1px dashed var(--border-strong); display: grid; place-items: center; font-size: 20px; margin-bottom: 10px` |
| `.empty h4` | `margin: 0; font-size: 14px` |
| `.empty p` | `margin: 5px 0 0; font-size: 13px; color: var(--text-2); line-height: 1.6` |
| `.stock-item` | `display: flex; gap: 12px; align-items: center; padding: 11px 0; border-bottom: 1px solid var(--border)` — last item: `border: 0; padding-bottom: 2px` |
| `.thumb` | `width: 38px; height: 38px; border-radius: 9px; background: var(--bg-2); border: 1px solid var(--border); display: grid; place-items: center; font-size: 16px; flex-shrink: 0` |
| `.stock-item b` | `font-size: 13.5px; display: block; line-height: 1.2` |
| `.stock-item span` | `font-size: 12px; color: var(--text-3)` |
| `.stock-bar` | `height: 5px; background: var(--bg-2); border-radius: 999px; overflow: hidden; margin-top: 6px; width: 120px` |
| `.stock-bar i` | `display: block; height: 100%; background: #f59e0b; border-radius: 999px` — fill width inline per item: `width: 30%` |
| `.qty` | `margin-left: auto; text-align: right` |
| `.qty b` | `font-size: 13px` |
| `.qty span` | `display: block; font-size: 11px; color: var(--amber); font-weight: 700; letter-spacing: .04em; text-transform: uppercase; margin-top: 1px` |
| Low Stock panel body override | `padding-top: 6px` (inline, over `.panel-body`) |
| Manage inventory button (inline) | `width: 100%; margin-top: 14px; border: 1px solid var(--border); background: var(--bg); border-radius: 10px; font-weight: 600; font-size: 13px` |

## 8. User Stories — Phase 2

| Element | Values |
|---|---|
| `.stories-hero` | `background: var(--card); border: 1px solid var(--border); border-radius: 16px; padding: 20px; box-shadow: var(--shadow); display: grid; grid-template-columns: 1.4fr .6fr; gap: 20px; margin-bottom: 14px` |
| `.stories-hero h2` | `margin: 0; font-size: 20px; letter-spacing: -.03em; line-height: 1.1` |
| `.stories-hero p` | `margin: 8px 0 0; color: var(--text-2); font-size: 13.5px; line-height: 1.6` |
| `.hero-stats` | `display: grid; grid-template-columns: 1fr 1fr; gap: 10px` |
| `.hero-stat` | `background: var(--bg); border: 1px solid var(--border); border-radius: 12px; padding: 12px` |
| `.hero-stat b` | `font-size: 20px; letter-spacing: -.03em; display: block` |
| `.hero-stat span` | `font-size: 11px; letter-spacing: .06em; text-transform: uppercase; color: var(--text-3); font-weight: 600` |
| `.filter-row` | `display: flex; flex-wrap: wrap; gap: 8px; margin: 16px 0 14px` |
| `.filter-row button` | `border: 1px solid var(--border); background: var(--card); border-radius: 999px; padding: 0 14px; font-size: 13px; font-weight: 600; color: var(--text-2); min-height: 38px` |
| `.filter-row button.active` | `background: #0f172a; color: #fff; border-color: #0f172a` — dark: `background: #fff; color: #0f172a` |
| `.stories-grid` | `display: grid; grid-template-columns: 1fr 1fr; gap: 14px` |
| `.story` | `background: var(--card); border: 1px solid var(--border); border-radius: 16px; box-shadow: var(--shadow); overflow: hidden; display: flex; flex-direction: column` |
| `.story-head` | `padding: 16px 18px 0; display: flex; justify-content: space-between; gap: 10px` |
| `.story-id` | `font-family: "JetBrains Mono", monospace; font-size: 11px; font-weight: 700; letter-spacing: .06em; color: var(--text-3)` |
| `.story h3` | `margin: 6px 18px 0; font-size: 15px; letter-spacing: -.015em; line-height: 1.25` |
| `.story-quote` | `margin: 10px 18px 0; background: var(--bg); border: 1px solid var(--border); border-radius: 10px; padding: 12px 13px; font-size: 13.5px; line-height: 1.6` |
| `.story-quote b` | `color: var(--accent)` |
| `.ac` | `padding: 12px 18px 16px` |
| `.ac-title` | `font-size: 11px; letter-spacing: .07em; text-transform: uppercase; font-weight: 700; color: var(--text-3); margin-bottom: 8px` |
| `.ac ul` | `list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 7px` |
| `.ac li` | `display: flex; gap: 9px; font-size: 13px; line-height: 1.55; color: var(--text-2)` |
| `.ac li::before` | `content: "✓"; width: 18px; height: 18px; flex-shrink: 0; border-radius: 999px; background: var(--accent-bg); color: var(--accent); display: grid; place-items: center; font-size: 11px; font-weight: 800; margin-top: 1px` |
| `.story-foot` | `margin-top: auto; border-top: 1px solid var(--border); padding: 10px 18px; display: flex; justify-content: space-between; gap: 10px; flex-wrap: wrap; font-size: 12px; color: var(--text-3); background: var(--card-2)` |
| `.priority` | `font-size: 11px; font-weight: 700; padding: 4px 8px; border-radius: 999px; white-space: nowrap` |
| `.p-high` | Light: `background: #fef2f2; color: #b91c1c; border: 1px solid #fecaca` — Dark: `background: #2b1414; color: #fca5a5; border-color: #4a1f1f` |
| `.p-med` | Light: `background: #fffbeb; color: #92400e; border: 1px solid #fde68a` — Dark: `background: #2a2110; color: #fcd34d; border-color: #443612` |

## 9. Hardcoded and inline values

These values are set in the stylesheet outside the tokens, in inline `style=""` attributes, or in SVG attributes. They do not change when a token changes, so port them explicitly.

| Where | Value |
|---|---|
| Logo, segmented active, filter active, primary buttons | Background `#0f172a`, text `#fff` — inverted in dark to background `#fff`, text `#0f172a` |
| `.avatar` background | `#e2e8f0` (no dark override) |
| `.btn-refresh` shadow | `0 4px 14px rgba(16,185,129,.25)` |
| `.live-dot` background | `#10b981` |
| Revenue chart line, gradient, end dot, legend dot | `#10b981` |
| Revenue chart end-dot stroke | `white` |
| Chart and donut axis / label text | `#94a3b8` |
| Donut cancelled arc | `#ef4444` |
| `.stock-bar i` fill | `#f59e0b`, width `30%` per low-stock item (inline) |
| `.p-high` / `.p-med` | Hardcoded hex, see section 8 — they duplicate the red/amber tokens in light mode but use different text colours in dark (`#fca5a5`, `#fcd34d`) |
| Search icon SVG stroke | `#94a3b8` (URL-encoded as `%2394a3b8`) |
| Top Products primary button (inline) | `background: #0f172a; color: #fff; border: 0; border-radius: 999px; padding: 0 14px; font-size: 13px; font-weight: 600` |
| Top Products secondary button (inline) | `background: var(--card); border: 1px solid var(--border); border-radius: 999px; padding: 0 14px; font-size: 13px; font-weight: 600; color: var(--text)` |
| Back to Overview button (inline) | `background: #0f172a; color: #fff; border: 0; border-radius: 999px; padding: 0 16px; font-weight: 600; font-size: 13.5px` |
| Export button (inline) | `border: 1px solid var(--border); background: var(--card); border-radius: 999px; padding: 0 12px; font-size: 13px; font-weight: 600` |
| "Live store" pill (inline) | `padding: 6px 10px` over `.pill` |
| Inventory nav pill (inline) | `margin-left: auto; background: var(--amber-bg); color: var(--amber); border-color: var(--amber-border)` |
| User Stories nav pill (inline) | `margin-left: auto` on `.pill.green` |
| Orders-by-status note box (inline) | `font-size: 12.5px; color: var(--text-2); line-height: 1.6; margin: 14px 0 0; background: var(--bg); border: 1px solid var(--border); border-radius: 10px; padding: 10px 12px` |
| Revenue panel stat labels (inline) | `font-size: 12px; color: var(--text-3); font-weight: 600; letter-spacing: .05em; text-transform: uppercase` |
| Revenue panel stat values (inline) | `font-size: 20px; font-weight: 700; letter-spacing: -.03em` |
| Definition of Done bar (inline) | `background: var(--card); border: 1px solid var(--border); border-radius: 14px; padding: 16px 18px; margin-top: 14px; box-shadow: var(--shadow); display: flex; gap: 16px; flex-wrap: wrap; justify-content: space-between; align-items: center` |
| Footer note under panels (inline) | `font-size: 12px; color: var(--text-3); margin-top: 16px; line-height: 1.6` |
| Alert icon (inline) | `font-size: 18px` |
| Alert badge (inline) | `margin-left: auto; white-space: nowrap` |

## 10. Responsive breakpoints

### At or below 980px — `@media (max-width: 980px)`

| Element | Changed value |
|---|---|
| `.kpi-grid` | `grid-template-columns: 1fr 1fr` |
| `.grid-2`, `.grid-2b`, `.stories-hero` | `grid-template-columns: 1fr` |
| `.stories-grid` | `grid-template-columns: 1fr` |
| `.sidebar` | `display: none` |
| `.main` | `padding: 0 16px 28px` |
| `.topbar` | `margin: 0 -16px; padding: 0 16px` |
| `.search` | `display: none` |

### At or below 560px — `@media (max-width: 560px)`

| Element | Changed value |
|---|---|
| `.kpi-grid` | `grid-template-columns: 1fr` |
| `.donut-flex` | `flex-direction: column; align-items: flex-start` |
| `.page-head h1` | `font-size: 23px` |
| `.view-tabs` | `gap: 2px` |
| `.view-tabs button` | `padding: 9px 8px; font-size: 12.5px; line-height: 1.25; text-align: center` |
| `.view-tabs .pill` | `font-size: 10px; padding: 2px 5px` |
| `.ac li .mono` | `word-break: break-all` |
| `.story-quote .mono`, `.ac .mono` | `overflow-wrap: anywhere` |

## 11. Spacing scale in use

The dashboard does not use a named spacing token. These are the values that repeat:

| Use | Values |
|---|---|
| Grid gaps (KPI, panels, stories) | `14px` |
| Card padding | `18px` (KPI), `16px 18px 18px` (panel body), `16px 18px 12px` (panel head) |
| Section margins | `14px` below KPI grid, alert and two-column grids; `16px` below meta line; `20px` below view tabs |
| Button horizontal padding | `14px` – `16px` |
| Border width | `1px` everywhere, `2px` only for the active view-tab underline |
| Border radius scale | `8px`, `9px`, `10px`, `12px`, `14px` (`--radius`), `16px`, `999px` |

## 12. Copy-paste stylesheet

The dashboard's full `<style>` block, verbatim.

```css
:root{
  --bg:#f6f7f9; --bg-2:#eef0f3; --card:#ffffff; --card-2:#fcfcfd;
  --border:#e3e6eb; --border-strong:#d4d8e0;
  --text:#0f172a; --text-2:#475569; --text-3:#94a3b8;
  --accent:#0a7a5c; --accent-bg:#e6f4ef; --accent-hover:#096c52;
  --amber:#b45309; --amber-bg:#fef3c7; --amber-border:#fde68a;
  --red:#b91c1c; --red-bg:#fef2f2; --red-border:#fecaca;
  --blue:#1d4ed8; --blue-bg:#eff6ff;
  --shadow:0 1px 2px rgba(15,23,42,.05), 0 8px 24px rgba(15,23,42,.04);
  --radius:14px;
}
@media (prefers-color-scheme: dark){
  :root{
    --bg:#0b0e13; --bg-2:#0f131a; --card:#12161f; --card-2:#161b26;
    --border:#222836; --border-strong:#2e3648;
    --text:#f1f5f9; --text-2:#94a3b8; --text-3:#64748b;
    --accent:#10b981; --accent-bg:#0f2e25; --accent-hover:#34d399;
    --amber:#f59e0b; --amber-bg:#2a2110; --amber-border:#443612;
    --red:#f87171; --red-bg:#2b1414; --red-border:#4a1f1f;
    --blue:#60a5fa; --blue-bg:#16233d;
    --shadow:0 1px 2px rgba(0,0,0,.4), 0 8px 24px rgba(0,0,0,.25);
  }
}
*{box-sizing:border-box}
html,body{margin:0;padding:0;background:var(--bg);color:var(--text);font-family:Inter,system-ui,-apple-system,Segoe UI,Roboto,sans-serif;-webkit-font-smoothing:antialiased}
a{color:inherit;text-decoration:none}
button{font-family:inherit;cursor:pointer;min-height:40px}
.mono{font-family:"JetBrains Mono",monospace}

/* LAYOUT */
.shell{display:flex;min-height:100vh}
.sidebar{width:232px;flex-shrink:0;background:var(--card);border-right:1px solid var(--border);padding:20px 14px;display:flex;flex-direction:column;gap:6px;position:sticky;top:0;height:100vh}
.main{flex:1;min-width:0;padding:0 28px 36px}
.topbar{height:60px;display:flex;align-items:center;justify-content:space-between;border-bottom:1px solid var(--border);margin:0 -28px;padding:0 28px;background:color-mix(in srgb, var(--bg) 88%, transparent);backdrop-filter:blur(8px);position:sticky;top:0;z-index:20}
.brand{display:flex;align-items:center;gap:10px}
.logo{width:34px;height:34px;border-radius:9px;background:#0f172a;color:#fff;display:grid;place-items:center;font-weight:700;font-size:15px;letter-spacing:-.02em}
@media(prefers-color-scheme:dark){.logo{background:#fff;color:#0f172a}}
.brand b{font-size:14px;letter-spacing:-.02em;line-height:1}
.brand span{display:block;font-size:11px;color:var(--text-3);font-weight:500;margin-top:2px;letter-spacing:.02em;text-transform:uppercase}
.nav-label{font-size:11px;letter-spacing:.08em;text-transform:uppercase;color:var(--text-3);font-weight:600;padding:14px 10px 6px}
.nav-item{display:flex;align-items:center;gap:10px;padding:9px 10px;border-radius:9px;font-size:13.5px;font-weight:500;color:var(--text-2);border:1px solid transparent}
.nav-item:hover{background:var(--bg)}
.nav-item.active{background:var(--accent-bg);color:var(--text);border-color:var(--border)}
.nav-item .dot{margin-left:auto;font-size:11px;background:var(--red);color:#fff;min-width:20px;height:20px;border-radius:999px;display:grid;place-items:center;padding:0 6px;font-weight:700}
.nav-icon{width:18px;text-align:center;font-size:15px}
.sidebar-footer{margin-top:auto;background:var(--bg);border:1px solid var(--border);border-radius:12px;padding:12px}
.sidebar-footer p{margin:0;font-size:12.5px;line-height:1.5;color:var(--text-2)}
.avatar-row{display:flex;align-items:center;gap:10px;padding-top:14px;border-top:1px solid var(--border);margin-top:14px}
.avatar{width:32px;height:32px;border-radius:999px;background:#e2e8f0;display:grid;place-items:center;font-weight:700;font-size:12px}
.avatar-row b{font-size:13px;display:block}
.avatar-row span{font-size:12px;color:var(--text-3)}

/* HEADER AREA */
.page-head{display:flex;flex-wrap:wrap;gap:16px;align-items:flex-start;justify-content:space-between;padding:22px 0 16px}
.page-head h1{font-size:26px;letter-spacing:-.03em;margin:0;line-height:1.05;font-weight:700}
.page-head p{margin:6px 0 0;color:var(--text-2);font-size:13.5px;line-height:1.5;max-width:620px}
.controls{display:flex;flex-wrap:wrap;gap:8px;align-items:center}
.seg{display:flex;background:var(--card);border:1px solid var(--border);border-radius:999px;padding:3px;box-shadow:var(--shadow)}
.seg button{border:0;background:transparent;padding:0 14px;border-radius:999px;font-size:13px;font-weight:600;color:var(--text-2);min-height:36px}
.seg button.active{background:#0f172a;color:#fff}
@media(prefers-color-scheme:dark){.seg button.active{background:#fff;color:#0f172a}}
.btn-refresh{background:var(--accent);color:#fff;border:0;border-radius:999px;padding:0 16px;font-weight:600;font-size:13.5px;display:inline-flex;align-items:center;gap:7px;box-shadow:0 4px 14px rgba(16,185,129,.25)}
.btn-refresh:active{transform:scale(.98)}
.meta-line{display:flex;flex-wrap:wrap;gap:8px 14px;align-items:center;font-size:12.5px;color:var(--text-3);margin-bottom:16px}
.live-dot{width:7px;height:7px;background:#10b981;border-radius:999px;display:inline-block;box-shadow:0 0 0 4px var(--accent-bg)}

/* VIEW TABS */
.view-tabs{display:flex;gap:4px;border-bottom:1px solid var(--border);margin-bottom:20px;overflow:hidden}
.view-tabs button{border:0;background:transparent;padding:11px 14px;font-size:13.5px;font-weight:600;color:var(--text-2);border-bottom:2px solid transparent;margin-bottom:-1px;display:inline-flex;gap:8px;align-items:center;min-height:44px}
.view-tabs button.active{color:var(--text);border-bottom-color:var(--accent)}
.pill{font-size:11px;font-weight:700;padding:2px 7px;border-radius:999px;background:var(--bg-2);border:1px solid var(--border);color:var(--text-2)}
.pill.green{background:var(--accent-bg);color:var(--accent);border-color:transparent}

/* KPI */
.kpi-grid{display:grid;grid-template-columns:repeat(4,1fr);gap:14px;margin-bottom:14px}
.kpi{background:var(--card);border:1px solid var(--border);border-radius:var(--radius);padding:18px;box-shadow:var(--shadow)}
.kpi-top{display:flex;justify-content:space-between;align-items:center;margin-bottom:12px}
.kpi label{font-size:12px;letter-spacing:.06em;text-transform:uppercase;color:var(--text-3);font-weight:600}
.kpi-icon{width:32px;height:32px;border-radius:8px;background:var(--bg);border:1px solid var(--border);display:grid;place-items:center;font-size:15px}
.kpi-value{font-size:28px;letter-spacing:-.04em;font-weight:700;line-height:1}
.kpi-sub{margin-top:8px;font-size:12.5px;color:var(--text-2);line-height:1.5}
.kpi-sub b{color:var(--text)}
.trend{font-size:11px;font-weight:700;padding:3px 6px;border-radius:999px;background:var(--bg);border:1px solid var(--border)}

/* ALERT BANNER */
.alert{display:flex;gap:12px;align-items:flex-start;background:var(--amber-bg);border:1px solid var(--amber-border);border-radius:12px;padding:13px 14px;margin-bottom:14px}
.alert strong{font-size:13.5px}
.alert p{margin:2px 0 0;font-size:13px;color:var(--text-2);line-height:1.5}
.alert a{font-weight:600;text-decoration:underline;text-underline-offset:2px}

/* GRID PANELS */
.grid-2{display:grid;grid-template-columns:1.65fr .95fr;gap:14px;margin-bottom:14px}
.grid-2b{display:grid;grid-template-columns:1.15fr .85fr;gap:14px}
.panel{background:var(--card);border:1px solid var(--border);border-radius:var(--radius);box-shadow:var(--shadow);overflow:hidden}
.panel-head{display:flex;justify-content:space-between;align-items:flex-start;gap:12px;padding:16px 18px 12px;border-bottom:1px solid var(--border)}
.panel-head h3{margin:0;font-size:14.5px;letter-spacing:-.01em}
.panel-head p{margin:3px 0 0;font-size:12.5px;color:var(--text-3);line-height:1.4}
.panel-body{padding:16px 18px 18px}
.link{font-size:12.5px;font-weight:600;color:var(--accent);white-space:nowrap;padding:6px 0}

/* CHART */
.chart-wrap{padding:6px 2px 0}
.legend{display:flex;gap:16px;flex-wrap:wrap;font-size:12px;color:var(--text-2);margin-top:10px}
.legend i{width:8px;height:8px;border-radius:999px;display:inline-block;margin-right:6px}

/* DONUT */
.donut-flex{display:flex;gap:18px;align-items:center}
.donut{width:118px;height:118px;flex-shrink:0}
.status-list{flex:1;display:flex;flex-direction:column;gap:10px}
.status-row{display:flex;justify-content:space-between;align-items:center;font-size:13px;padding:8px 0;border-bottom:1px dashed var(--border)}
.status-row:last-child{border:0}
.badge{font-size:11px;font-weight:700;letter-spacing:.04em;text-transform:uppercase;padding:4px 8px;border-radius:999px}
.badge-red{background:var(--red-bg);color:var(--red);border:1px solid var(--red-border)}
.badge-amber{background:var(--amber-bg);color:var(--amber);border:1px solid var(--amber-border)}
.badge-gray{background:var(--bg);color:var(--text-2);border:1px solid var(--border)}

/* TABLE / LISTS */
.empty{ text-align:left; padding:8px 0 2px}
.empty-icon{width:44px;height:44px;border-radius:12px;background:var(--bg);border:1px dashed var(--border-strong);display:grid;place-items:center;font-size:20px;margin-bottom:10px}
.empty h4{margin:0;font-size:14px}
.empty p{margin:5px 0 0;font-size:13px;color:var(--text-2);line-height:1.6}
.stock-item{display:flex;gap:12px;align-items:center;padding:11px 0;border-bottom:1px solid var(--border)}
.stock-item:last-child{border:0;padding-bottom:2px}
.thumb{width:38px;height:38px;border-radius:9px;background:var(--bg-2);border:1px solid var(--border);display:grid;place-items:center;font-size:16px;flex-shrink:0}
.stock-item b{font-size:13.5px;display:block;line-height:1.2}
.stock-item span{font-size:12px;color:var(--text-3)}
.stock-bar{height:5px;background:var(--bg-2);border-radius:999px;overflow:hidden;margin-top:6px;width:120px}
.stock-bar i{display:block;height:100%;background:#f59e0b;border-radius:999px}
.qty{margin-left:auto;text-align:right}
.qty b{font-size:13px}
.qty span{display:block;font-size:11px;color:var(--amber);font-weight:700;letter-spacing:.04em;text-transform:uppercase;margin-top:1px}

/* STORIES */
.stories-hero{background:var(--card);border:1px solid var(--border);border-radius:16px;padding:20px;box-shadow:var(--shadow);display:grid;grid-template-columns:1.4fr .6fr;gap:20px;margin-bottom:14px}
.stories-hero h2{margin:0;font-size:20px;letter-spacing:-.03em;line-height:1.1}
.stories-hero p{margin:8px 0 0;color:var(--text-2);font-size:13.5px;line-height:1.6}
.hero-stats{display:grid;grid-template-columns:1fr 1fr;gap:10px}
.hero-stat{background:var(--bg);border:1px solid var(--border);border-radius:12px;padding:12px}
.hero-stat b{font-size:20px;letter-spacing:-.03em;display:block}
.hero-stat span{font-size:11px;letter-spacing:.06em;text-transform:uppercase;color:var(--text-3);font-weight:600}
.filter-row{display:flex;flex-wrap:wrap;gap:8px;margin:16px 0 14px}
.filter-row button{border:1px solid var(--border);background:var(--card);border-radius:999px;padding:0 14px;font-size:13px;font-weight:600;color:var(--text-2);min-height:38px}
.filter-row button.active{background:#0f172a;color:#fff;border-color:#0f172a}
@media(prefers-color-scheme:dark){.filter-row button.active{background:#fff;color:#0f172a}}
.stories-grid{display:grid;grid-template-columns:1fr 1fr;gap:14px}
.story{background:var(--card);border:1px solid var(--border);border-radius:16px;box-shadow:var(--shadow);overflow:hidden;display:flex;flex-direction:column}
.story-head{padding:16px 18px 0;display:flex;justify-content:space-between;gap:10px}
.story-id{font-family:"JetBrains Mono",monospace;font-size:11px;font-weight:700;letter-spacing:.06em;color:var(--text-3)}
.story h3{margin:6px 18px 0;font-size:15px;letter-spacing:-.015em;line-height:1.25}
.story-quote{margin:10px 18px 0;background:var(--bg);border:1px solid var(--border);border-radius:10px;padding:12px 13px;font-size:13.5px;line-height:1.6}
.story-quote b{color:var(--accent)}
.ac{padding:12px 18px 16px}
.ac-title{font-size:11px;letter-spacing:.07em;text-transform:uppercase;font-weight:700;color:var(--text-3);margin-bottom:8px}
.ac ul{list-style:none;margin:0;padding:0;display:flex;flex-direction:column;gap:7px}
.ac li{display:flex;gap:9px;font-size:13px;line-height:1.55;color:var(--text-2)}
.ac li::before{content:"✓";width:18px;height:18px;flex-shrink:0;border-radius:999px;background:var(--accent-bg);color:var(--accent);display:grid;place-items:center;font-size:11px;font-weight:800;margin-top:1px}
.story-foot{margin-top:auto;border-top:1px solid var(--border);padding:10px 18px;display:flex;justify-content:space-between;gap:10px;flex-wrap:wrap;font-size:12px;color:var(--text-3);background:var(--card-2)}
.priority{font-size:11px;font-weight:700;padding:4px 8px;border-radius:999px;white-space:nowrap}
.p-high{background:#fef2f2;color:#b91c1c;border:1px solid #fecaca}
.p-med{background:#fffbeb;color:#92400e;border:1px solid #fde68a}
@media(prefers-color-scheme:dark){.p-high{background:#2b1414;color:#fca5a5;border-color:#4a1f1f}.p-med{background:#2a2110;color:#fcd34d;border-color:#443612}}
.search{flex:1;max-width:340px;margin-left:20px}
.search input{width:100%;height:38px;border-radius:999px;border:1px solid var(--border);background:var(--card);padding:0 14px 0 36px;font-size:13.5px;color:var(--text);background-image:url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' viewBox='0 0 24 24' fill='none' stroke='%2394a3b8' stroke-width='2'%3E%3Ccircle cx='11' cy='11' r='7'/%3E%3Cpath d='M21 21l-4.3-4.3'/%3E%3C/svg%3E");background-repeat:no-repeat;background-position:12px center}

@media(max-width:980px){
  .kpi-grid{grid-template-columns:1fr 1fr}
  .grid-2,.grid-2b,.stories-hero{grid-template-columns:1fr}
  .stories-grid{grid-template-columns:1fr}
  .sidebar{display:none}
  .main{padding:0 16px 28px}
  .topbar{margin:0 -16px;padding:0 16px}
  .search{display:none}
}
@media(max-width:560px){.kpi-grid{grid-template-columns:1fr}.donut-flex{flex-direction:column;align-items:flex-start}.page-head h1{font-size:23px}
.view-tabs{gap:2px}.view-tabs button{padding:9px 8px;font-size:12.5px;line-height:1.25;text-align:center}.view-tabs .pill{font-size:10px;padding:2px 5px}
.ac li .mono{word-break:break-all}
.story-quote .mono,.ac .mono{overflow-wrap:anywhere}
}
```
