# Slice P2.71 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings · Api.IntegrationTests 617/617 · jest 2111/2111

This is a faithful move. Each handler is the controller's action line for line: validate, call one service, map the outcome. The no-store filter covers exactly the responses the MVC action filter covered. The AE pins were green on MVC before you changed anything. I re-ran six of your mutations and every one went red for the right reason.

### Checks I ran myself
- **Queries per request.** Each handler makes one service call (`RegisterAsync`, `LoginAsync`, `RefreshAsync`, `LogoutAsync`, `RequestAsync`, `ConfirmAsync`, `GetAsync`), and only after validation passes. The endpoints file has no `DbContext`, repository or user lookup, so there is no N+1 and no extra round-trip. `me` does one read.
- **Secrets in logs.** `AuthEndpoints.cs` has no logger and no interpolation of request values. II13 is green, and `RefreshService` still logs the reuse warning without the token.
- **Constant-time login.** The handler never branches on whether the user exists. Every non-success outcome maps to the same 401, and AE2 pins the exact strings for the unknown-email, wrong-password and locked cases.
- **Policies.** Six routes are `.AllowAnonymous()` and `me` is `.RequireAuthorization()`. The group has no policy and is built from `app`, and there is no `MapGet("")`. The inventory and II11 are unedited and green.
- **Cookie.** The cookie is written only in cookie mode. It is expired on a failed refresh and on logout, in cookie mode only. In cookie mode the body never carries the refresh token (`TokenResponse.From(session, cookie)`).
- **Dead code.** `ToProblem` and the MVC `using` are gone from `AuthValidators.cs`. `MapIdentityEndpoints` is the only new public member. `ToErrors` is five plain lines.
- **Mutations (6 of 8, re-run from scratch copies, `cmp` ok after each):**

| # | Break | Red |
|---|---|---|
| M1 | `me` → `.AllowAnonymous()` | T1 inventory (`me \| - \| anon`), II11, AE3 (no `Bearer` challenge) |
| M2 | no `NoStore` filter | II1, T4, AE1, AE3 (`Expected "no-store, no-cache"`, `Actual null`) |
| M3 | `Secure = false` | AE5 |
| M5 | delete `family.Revoke(Reuse, now)` | II4 (`Expected Unauthorized`, `Actual OK`), II5, AE5 (`Expected Reuse`, `Actual null`) |
| M7 | drop logout `.Accepts` | AE6 (`Expected NoContent`, `Actual NotFound`) |
| M8 | drop refresh `.Accepts` | AE6 (`Expected OK`, `Actual NotFound`) |

### 1. A login/register/password-reset request without a JSON Content-Type now gets 404 — NOTE, owned by P2.82   (`AuthEndpoints.cs:19–20, 24–25`)
**What I saw:** I built a scratch net10 probe (MVC controller, minimal route and an anonymous `MapFallback`, as in the host) and called it with curl. A required `[FromBody]` route answers **404** when the request has no `Content-Type`, with or without a body, and also when the body is sent as `text/plain`. MVC answered **415** in all three cases. Your Amendment 1 fix covers only `refresh` and `logout`. `register`, `login`, `password-reset` and `password-reset/confirm` behave like the P2.68–P2.70 routes.
**Why it matters:** a client that forgets the header gets "not found" for a route that exists, so it looks like a routing bug instead of a media-type error. First-party clients always send JSON, so neither tokens nor money are affected.
**What I want:** nothing in this slice. P2.82 fixes it once, at the host, for every body route, Identity included. Its central convention also makes your per-route `.Accepts` on lines 21–23 redundant, so P2.82 removes them. AE6, M7 and M8 then guard the convention.
**How I'll check:** P2.82's pins and its structural test.
**Lesson:** "no-body" pins need a no-Content-Type twin, because routing rejects the request before binding ever sees it.

### 2. AE4 only counts `Verifies` — NOTE (backlog)
`register {}` would hash, not verify. The "no user row" assert already catches a register that reaches the service, so this is not a gap, only a slightly misleading name for the counter check.

**What was good:** the comment on the `NoStore` filter states why the headers go before `next`. It names the guarantee, not the mechanism. Root-causing AE6 down to `AcceptsMatcherPolicy` plus the fallback, instead of reaching for `EmptyBodyBehavior`, is what found the P2.68–P2.70 bug.
