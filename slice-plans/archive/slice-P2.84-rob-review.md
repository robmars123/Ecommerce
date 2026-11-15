# Slice P2.84 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — jest 274 suites, 2127 tests.

Small, plain change that matches the plan: the footer splits into a container, the unchanged accessible name/role group, and a sibling `ActionButton` driven by the existing `useLogoutMutation`. No new hook, no navigation code, no change to `useLogoutMutation`/`session.ts` (`git status` on `features/auth` is clean). No blocking findings.

**Mutations (re-run by me, scratch copy, restored, `cmp` clean):**
- M1 delete `await endSession();` → `Tests: 10 failed, 10 total` (O4–O7 red, both projects).
- M2 delete `queryClient.clear();` → `Tests: 2 failed, 8 passed, 10 total` (O4 red).

**Changed tests (checked as possibly weakened):** the seven helper renders only gained a `QueryClientProvider` wrapper with a fresh `createTestQueryClient()`; no assertion changed. S26 lost a claim that is no longer true and gained `expect(fetchSpy).not.toHaveBeenCalled()` — stronger. Accepted.

**Hunt (round-trips, over-fetch, edge cases):** one POST per press; logout does not refetch anything (gate unmounts the admin tree before observers can rebuild cleared queries); wide sidebar and narrow overlay are exclusive, so there is only one button mounted (O2). `ActionButton` stretches in the column by default, no new variant.

### Your concerns, by number

1. **`returnTo=/admin` instead of the current page — Accepted, NOTE.** Not a bug: `safeReturnTo` keeps it same-app and the gate re-checks the role after the next sign-in. On a shared device `/admin` is, if anything, the better landing for the next person. Pinning `returnTo: '/admin'` in O4 is fine because the comment says why; if Nathaniel wants a plain `/login`, that is a new row (your backlog line stands).
2. **Four extra renders wrapped — Accepted.** They mount the footer, so they need a client exactly like the real app; no assertion touched.
3. **O4 counts `QueryClient.prototype.clear` = 2 — Accepted, NOTE.** M2 goes red, which is what the plan asked of O4. It is a proxy (call count, not "no admin data left"), so a stronger version is in the backlog: spy `createQueryClient` from `@/lib/query-client` to capture the root client and assert the dashboard query is `undefined` after logout.
4. **O6 same-tick double press — Accepted.** Two real taps are two separate touch sequences; each press is a discrete event and React flushes the `pending` render between them, so the button is disabled before the second `onPress`. Worst case a second POST hits an already revoked token and is swallowed by the best-effort `mutationFn`; `endSession()` is idempotent. Same pattern as `AccountBar` and every other `ActionButton`.

### 5. Casts around `mockFetchRoutes` — NOTE (`tests/admin-logout.test.tsx:25,79`)
**What I saw:** `logoutRoute as () => Response` and `held as unknown as Response`, because `mockFetchRoutes` types routes as `() => Response` although it already awaits them.
**What I want (later):** widen the route type to `() => Response | Promise<Response>` in `tests/fetch-mock.ts` and drop both casts. Backlog.
**Lesson:** when a test needs a cast to say something true, fix the helper's type, not the call site.

**What was good:** you put the button outside the `accessible` group and proved it with O1 (`within(group).queryByRole` is null) — that is the screen-reader rule, tested. And you ran the AdminShell file green on the wrapper change before touching production code, so the 7-helper change is attributable.
