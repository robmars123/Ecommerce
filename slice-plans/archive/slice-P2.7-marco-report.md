# Slice P2.7 - Marco report

Concerns:
- Under fake timers `Response.text()` and TanStack's zero-delay notify do not settle by advancing fake time alone. I added one test helper `frontend/apps/store/tests/settle.ts` (`settleFetch`: a short real-clock wait, then `advanceTimersByTimeAsync(0)`). Not named in the plan; two callers (AdminSearch.test, AdminShell.test). `useDebouncedValue` is not mocked; no real-timer `waitFor` sleeps.
- Pre-P2.8 the live API 404s, so the box shows its fixed error state. Not eyeballed on :8081 (no browser run); manual check added to backlog for Nathaniel.
- Typing 1 char after a valid term shows the previous term's results for up to 300 ms (the debounced term lags). Minor, accepted.

Bugs found: None
Bugs fixed: None
Bugs open: None

Tests changed: AdminShell.test.tsx - added imports (`act`, fixtures, `renderRouterWithQuery`, `settleFetch`), helper `renderShellWithQueryAt` and `describe('AdminShell search')` S26-S29. S1-S25 and existing helpers untouched. query-keys.test.ts - K1 appended.
New tests: Q1-Q5 (search.test.ts), A1-A2 (api.test.ts), C1-C11 (AdminSearch.test.tsx; C4 and C7 each one `it`), fixture `tests/admin-search-fixtures.ts`.

Red first (before any implementation, `npx jest features/admin-shell lib/query-keys`):
- `Cannot find module './search' from 'features/admin-shell/search.test.ts'` (Q1-Q5 incl. Q3)
- `Cannot find module './AdminSearch' from '.../AdminSearch.test.tsx'` (C1-C11 incl. C8)
- `Cannot find module './api'` (A1-A2); K1 and S26-S29 failed (no `queryKeys.admin.search`, no `Search admin` label)
- `Tests: 10 failed, 150 passed, 160 total`
Green after build: `Tests: 212 passed, 212 total` (admin-shell + query-keys, ios+android projects).

Changed (slice P2.7, frontend only; no backend):
- New: features/admin-shell/{types.ts, api.ts, search.ts, api.test.ts, search.test.ts}, features/admin-shell/components/{AdminSearch.tsx, AdminSearch.test.tsx}, tests/{admin-search-fixtures.ts, settle.ts}
- Edited: features/admin-shell/components/AdminShell.tsx (wide: breadcrumb flex-1 + search column; phone: third row), AdminShell.test.tsx, lib/query-keys.ts (+test), slice-plans/backlog.md (one line)
- Tracker not touched (per instruction).

Token check: grep `#[0-9a-fA-F]{3,8}|rgb|style=` over the new non-test files -> no matches (rc=1).
Support filter: `searchGroups` shows Products/SKUs only when `adminRouteDecision(role,'/admin/products') === null`; Orders likewise for `/admin/orders`; Q3, Q5, C8 cover it.

verify.sh:
PASS build 0 Error(s)|0 Warning(s)
PASS jest Test Suites: 189 passed, 189 total|Tests: 1445 passed, 1445 total
VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)

Quality-bar §2: all boxes met for a Low-tier frontend slice. Tests-first shown; no secrets; no backend/DB/money change (N/A); theme tokens only (one JS-prop exception `placeholderTextColor`, as in SearchBar); no new packages; no test weakened. Manual 1280/390 px visual check: not done, in backlog.

## Round 2 — fix round 1 (S29)

Fixed: `renderShellWithQueryAt` in AdminShell.test.tsx now mounts one `<AdminShell><Slot/></AdminShell>` inside a `_layout` (with a QueryClientProvider), routes are plain stubs, so navigation no longer remounts the search box. S29 keeps its assertions and adds `expect(getByLabelText('Search admin').props.value).toBe('')` after the sidebar navigation. No product code changed; no real bug exposed.

Mutation (CLAUDE.md §5): AdminSearch.tsx:58 `useEffect(reset, [pathname])` replaced by a comment. `jest AdminShell.test.tsx -t "S28|S29"`: `Tests: 2 failed, 56 skipped, 2 passed` (2 jest projects: S29 red in both, with `expect(screen.queryByLabelText('Search results')).toBeNull()` receiving the Search results view; S28 green). Restored from scratch copy; `cmp` identical (CMP_OK).

Tests changed: S29 helper and one added assertion (strengthened, nothing weakened). Imports: QueryClientProvider, Slot, createTestQueryClient replace renderRouterWithQuery.

verify.sh changed: VERIFY PASS (secrets hygiene tsc jest export); jest 189 suites / 1445 tests. `jest features/admin-shell`: 12 suites, 184 tests passed.
