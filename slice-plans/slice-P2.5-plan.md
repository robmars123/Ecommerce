# Slice P2.5 — Chart package approval (`react-native-svg`)

Tracker: `docs/admin-dashboard-feature-tracker.md` row P2.5. Risk tier: **Low** (no code, no money/stock/auth path).

**No build step: approval only.** P2.5 changes no file under `frontend/` or `backend/`. The coordinator can skip Marco; there is nothing to review beyond this plan and the one decision-log row.

## 1. Read

Nobody builds in P2.5. The P2.26 plan author (Rob) and builder (Marco) read:
- arch §2 (`docs/architecture/02-stack-lock.md`) — frontend row and "Frontend delivery" (no committed `android/`/`ios/`, EAS cloud builds, Expo Go testing).
- arch §3.4 rule 16 (`03-principles-hard-rules.md:49`) — no unplanned dependency.
- arch §5.5 (`05-frontend.md`) — tokens, the `style`/JS-prop exception for colour props without a `className` mapping, platform variants.
- decision log (`13.2-decision-log.md`), grep `Expo SDK patch alignment` and `Chart package` (the row added by this slice).
- this plan, §2–§3.

## 2. Package approval (arch §3.4 rule 16)

| Field | Answer |
|---|---|
| **Name** | `react-native-svg` — one runtime dependency, nothing else (no chart library on top of it). |
| **Version** | Whatever Expo SDK 58 recommends, installed with `npx expo install react-native-svg` from `frontend/apps/store`, then pinned **exact** in `package.json` like every other package (no `^`/`~`). The SDK decides the version; `verify.sh` `expo-check` (`npx expo install --check`) must stay green (decision log, "Expo SDK patch alignment"). |
| **Why** | P2.26 (revenue trend line chart), P2.27 (orders-by-status donut) and P2.42 (analytics trend) need lines, arcs and filled paths. React Native has no vector drawing primitive; `View` can only make rectangles (and circles via border radius), so a line chart with sloped segments and a donut with proportional arcs cannot be drawn correctly from core components. `react-native-svg` gives `Svg`, `Path`, `Circle`, `Line`, `G`, `Text` on iOS, Android and web (on web it renders real DOM `<svg>` through React Native Web), from one codebase as §2 requires. |
| **What it replaces** | Nothing currently installed — it is the first drawing dependency. It **avoids** two alternatives, both **not approved**: (a) hand-built `View`-based bars/segments (cannot draw a sloped line or an arc; rotated-`View` hacks render differently per platform and are untestable by role); (b) a full chart library — `victory-native` (also pulls in Skia + Reanimated-heavy code and its own native module), `react-native-chart-kit`, `react-native-gifted-charts` or similar. Any of those needs its own approval in a Rob plan; none is approved here. |
| **Native folders** | None. `react-native-svg` is in the Expo SDK's module set: it ships prebuilt in Expo Go and is autolinked during cloud EAS Build prebuild. No config plugin entry is required, no `android/`/`ios/` folder is generated in the repo, no Kotlin/Swift (§2 "Frontend delivery" unchanged). |
| **Stack lock** | Not a stack change: no new service, framework or platform; it is a rendering library inside the locked Expo + RN Web frontend. §2 needs no amendment; the approval is recorded as one decision-log row (§13.2). |
| **Dev-only?** | No — runtime dependency (it ships in the bundle). Web bundle cost is small (thin wrapper over DOM `<svg>`); checked by the `export` step in P2.26. |

## 3. Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| Approve `react-native-svg` only, at the SDK-recommended version, pinned exact | Smallest primitive that draws lines and arcs on all three targets; SDK pin keeps Expo Go / EAS native ABI aligned | A chart library (larger, more native code, own approval needed); `View` hacks (can't draw arcs/slopes) |
| **Install deferred to P2.26** (first consumer) | CLAUDE.md "nothing speculative" and the plan rule "every abstraction must have a consumer in this slice": installing now adds an unused dependency to `package.json` and the bundle with no test exercising it | Install in P2.5 — would be dead weight for a slice with no consumer, and its first real test would land in a different review anyway |
| Charts drawn by our own small components in `features/admin-dashboard` (P2.26/27) and reused by `admin-analytics` (P2.42) | Two shapes (polyline, donut arcs) are ~tens of lines each; we own the empty state and accessibility | Generic chart abstraction now — no consumer in P2.5 |
| Chart colours come from `lib/design-tokens.js`, passed as `stroke`/`fill` props | `react-native-svg` colour props have no `className` mapping — the §5.5 JS-prop exception covers exactly this; no hex in `features/` | Hard-coded hex in chart components (breaks §5.5 one token source) |

## 4. Files

P2.5 writes only:
- `slice-plans/slice-P2.5-plan.md` (this file).
- `docs/architecture/13.2-decision-log.md` — one appended row, "Chart package (tracker P2.5, Rob)".

No migrations, no module contracts, no `frontend/` or `backend/` change.

## 5. Build order and tests

None in P2.5 (approval only). Obligations handed to **P2.26** (its plan must cite this file and the decision-log row, and adds these):
1. Install: `cd frontend/apps/store && npx expo install react-native-svg`; pin exact; `package-lock.json` updated by npm only.
2. `bash scripts/verify.sh` full: `expo-check` green (SDK-aligned version), `export` green (web bundle builds with the SVG components).
3. Jest: render the chart through `jest-expo`; if `react-native-svg` is not already transformed by the preset's `transformIgnorePatterns`, add it there — a config change, not a mock that hides rendering. Tests assert by role/label (§11.3): chart has an `accessibilityLabel` summarising the data, plus the empty state; no class or SVG-path string assertions.
4. Marco's report quotes the installed version and the `expo-check` line.

P2.27 and P2.42 reuse the installed package; they need no new approval, but any **additional** chart/drawing package does.

## 6. Risks

| Risk | Mitigation |
|---|---|
| Expo publishes an SDK 58 patch moving the recommended `react-native-svg` version | `expo-check` turns red; align with `npx expo install --fix` in the next slice (decision log "Expo SDK patch alignment") |
| Jest cannot parse the package's ESM source | P2.26 step 3: transform it, don't mock it away |
| Web and native render text/stroke slightly differently | P2.26 checks web export plus Expo Go on a phone; tests stay behavioural |
| Scope creep to a chart library later | Rule 16: any further package needs its own Rob-plan approval; this row approves only `react-native-svg` |

## 7. After this slice

Coordinator marks tracker row P2.5 Done (deliverable is this approval). P2.26 is the next slice that touches `package.json`.
