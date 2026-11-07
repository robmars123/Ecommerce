# Slice 1b — Learning notes

Moved verbatim from `slice-1b-plan.md` §13 (for Nathaniel; agents do not read this file).

## 13. Learning notes (for Nathaniel)

### 13.1 Design choices in interview form

> *"I chose X because of these constraints, gave up Y, and would switch to Z when this metric changes."* (§13.0)

**1. Short JWT and opaque, rotating refresh token, not server sessions.**
- **Why:** the API checks the JWT with no DB call, which matters most for catalog-heavy reads. The DB-backed refresh token restores what pure JWTs lose: revocation, logout, and theft detection.
- **Gave up:** instant revocation of access tokens. A stolen access token works for up to 15 minutes, and logout doesn't kill it.
- **Rejected:**
  - *Server sessions with a cookie on every request* (simplest revocation, but a DB or Redis lookup per request, and awkward on native).
  - *Long-lived JWTs* (no revocation at all).
  - *A JWT refresh token* (you still need a DB row to rotate and revoke it, so the JWT format adds nothing but size).
- **Switch when:** the 15-minute window isn't acceptable (for example admin actions after a firing). Then add a per-request security-stamp or `jti` denylist check for admin routes only, or shorten admin access tokens.

**2. Token families and strict reuse detection, with no grace window.**
- **Why:** if an attacker and the real user both hold the same refresh token, the second use of a consumed token proves that two parties have it. Revoking the family logs both out, which the attacker can't undo.
- **Gave up:** tolerance for a lost response. If the response to a refresh is lost and the client retries, the user is logged out.
- **Rejected:**
  - *Rotation without reuse detection* (a stolen token silently forks the session).
  - *A reuse interval of N seconds* (convenient, but an attacker who replays within N seconds wins).
- **Switch when:** forced-logout reports from flaky mobile networks are measured. Then add a short interval that returns the **same** child, not a new branch.

**3. Web refresh token in an httpOnly, SameSite=Strict cookie with a custom-header CSRF guard; native in SecureStore.**
- **Why:** the most damaging XSS outcome is stealing a 30-day credential, and script can't read an httpOnly cookie. The custom header forces a CORS preflight, so other origins can't ride the cookie.
- **Gave up:** one uniform transport, so the API now has two code paths. Production also needs the web app and API on the same site.
- **Rejected:**
  - *localStorage* (any XSS exfiltrates it).
  - *A cookie for native too* (native cookie jars are unreliable, and SecureStore is better there).
  - *`SameSite=None`* (reopens CSRF and needs more defenses).
- **Switch when:** the chosen domains are cross-site. Then use a backend-for-frontend on the web origin, or `SameSite=None` plus explicit CSRF tokens.

**4. Pessimistic locks for auth, optimistic versions for admin edits.**
- **Why:** a login is a millisecond critical section, where "lock, check, write" is easy to prove. An admin edit spans minutes of human think time, so you can't hold a lock across it. You **detect** the lost update with `xmin` and ask the second editor to reload.
- **Gave up:** auth throughput on a single hot account, and a smoother "merge" UX for editors.
- **Rejected:**
  - *Optimistic concurrency for lockout* (a conflict either loses a count or needs a retry loop, and verifying before the write lets parallel guesses through).
  - *Last-write-wins for admin edits* (silent data loss).
- **Switch when:** login p95 under load is dominated by lock waits. Then move the counter to an atomic `UPDATE … SET count = count + 1 RETURNING` and keep the verify-after-reserve order.

**5. Own user store plus Identity's password hasher, not `UserManager`.**
- **Why:** the hard parts that must be standard (PBKDF2 parameters, salt, format versioning, rehash on upgrade) stay Microsoft's. The parts that must fit **our** invariants (one DbContext, module-internal entities, `IClock`, row locks) are ours.
- **Gave up:** Identity's built-in features (2FA, external logins, email confirmation tokens) for free, plus some extra code (about 30 lines of lockout logic).
- **Rejected:**
  - *Full Identity EF stores* (they need framework entity types in our model and a Data Protection key ring).
  - *A hosted identity provider* (§13.1, less learning and another vendor).
- **Switch when:** SSO, social login, or 2FA lands. Re-evaluate Identity's full stack or an OIDC provider then.

**6. Fallback authorization policy, "authenticated unless `[AllowAnonymous]`".**
- **Why:** the failure mode of a forgotten attribute becomes 401, not a public admin endpoint. The endpoint-inventory test makes every new anonymous endpoint a reviewed decision.
- **Gave up:** a little ceremony on public endpoints.
- **Rejected:** opt-in `[Authorize]` (fails open).
- **Switch:** never. It is a pure safety default.

**7. Strict JSON binding (unknown fields → 400) plus explicit DTOs.**
- **Why:** mass assignment (`"role":"Admin"`, `"isActive":true`) fails loudly, and a contract drift between client and server shows up in tests.
- **Gave up:** lenient forward compatibility. Older clients that send extra fields break.
- **Switch when:** a public API with third-party clients appears. Then accept and ignore unknown fields, but only on versioned DTOs.

**8. Password reset through a sender seam called after commit, then the outbox in Slice 6.**
- **Why:** there is no provider, so nothing is invented, and rule 6 holds (the state commits first, with no external call).
- **Gave up:** guaranteed delivery. If the process crashes between commit and send, the email is lost; the user just asks again.
- **Rejected:** storing the plaintext token for a later sender (a DB read becomes account takeover).
- **Switch when:** Slice 6 lands. Then the consumer mints the token at send time.

### 13.2 Failure modes this slice defends against

| Failure | Attack or bug | Defense here | Proven by |
|---|---|---|---|
| Access-token theft | An XSS, log, or proxy leak replays a JWT | 15 min lifetime; memory-only storage; never logged; alg pinned, key ≥ 256 bits | II-12, II-13, F2b |
| Refresh-token theft | A stolen refresh token used in parallel with the victim's | Rotation + family revocation on reuse; hashed at rest; absolute 30-day cap; httpOnly cookie on web, SecureStore on native | II-4, II-6, II-8, ID-2 |
| Refresh race (benign) | Two tabs or a retry refresh at once and look like theft | Client single-flight; server family lock makes the outcome deterministic (one winner, then the family is revoked) | S-1, II-5 |
| Lockout bypass | N parallel guesses verified before the counter updates | Row lock **before** verify, so at most 5 verifications | II-3, ID-1 |
| Lockout as an oracle | "Locked" or "no account" returns faster, or with different text | Dummy verify on both paths; identical 401 body | II-2, ID-5 |
| Enumeration via reset | "No such email" response | Always 202, same body; sender only for real users | II-9, V-5 |
| Enumeration via register | 409 on an existing email | **Accepted** (D9); Slice 7 rate limits | II-1 (documents it) |
| Privilege escalation | `"role":"Admin"` on register; bootstrap email squatting | Strict JSON plus explicit DTOs; bootstrap is create-only | II-1, II-15 |
| Missing per-endpoint authorization | A new admin endpoint without `[Authorize]` | Fallback policy plus the anonymous-endpoint inventory test | II-10, II-11 |
| Per-resource authorization | Reading another user's data | `me` reads only the caller's `sub`; orders arrive in Slice 5 (test 7) | II-10 |
| CSRF on the cookie | A cross-site POST to refresh or logout | SameSite=Strict, plus the cookie only honoured with `X-Auth-Transport` (forces preflight) | II-8, II-16 |
| Reset-token replay | Reusing or racing a reset link | Single use under a lock; newest-only; 30 min; all sessions revoked | II-9, ID-4 |
| Open redirect | `/login?returnTo=//evil` | `safeReturnTo` | R-1 |
| Lost admin edit | Two admins save over each other | `xmin` version → 409 | IA-2 |
| Secrets in git | A committed signing key or admin password | Empty placeholders; Dev ephemeral key; startup fails without a key outside Dev; CI scan | II-14, secret scan |

### 13.3 What to study, and why

1. **OAuth 2.0 Security BCP (RFC 9700), the refresh-token rotation section, and the OAuth 2.0 for Browser-Based Apps draft.** This is where "rotation plus reuse detection" and "keep refresh tokens out of JS" come from. Be able to explain why the family is revoked rather than just the token.
2. **RFC 8725 (JWT Best Current Practices).** Algorithm pinning, `alg: none`, key length, and `aud`/`iss` validation. Be able to explain algorithm confusion (HS256 with an RSA public key used as the secret).
3. **OWASP cheat sheets: Authentication, Session Management, Forgot Password, CSRF.** These cover the enumeration, timing, and reset-token rules used here.
4. **NIST SP 800-63B §5.1.1.** Why length beats composition rules, and why throttling matters more than complexity.
5. **PostgreSQL docs: "Explicit Locking" (row-level locks, deadlocks) and "Concurrency Control" (READ COMMITTED).** Be able to explain why `FOR UPDATE` serializes the lockout check, why lock order prevents deadlocks, and what `xmin` is.
6. **EF Core docs: "Handling Concurrency Conflicts" and the change tracker's identity resolution.** The stale-tracked-entity trap in D12 is a real production bug class.
7. **ASP.NET Core docs: authorization policies and the fallback policy, plus JwtBearer `TokenValidationParameters`.**
8. **MDN: `Set-Cookie` (SameSite, `__Host-` prefix, Secure), CORS with credentials and preflight.** Be able to explain why a custom header defeats form-based CSRF.

### 13.4 Concepts you should be able to explain cold

- Authentication versus authorization, and endpoint-level versus resource-level authorization.
- Why access tokens are short and stateless while refresh tokens are long and stateful, and the revocation trade-off between them.
- Rotation, families, and reuse detection, including why a benign double refresh looks identical to theft.
- Why a password hash must be slow (PBKDF2, bcrypt, Argon2) while a 256-bit random token can use SHA-256.
- Timing side channels in login, and how a dummy hash equalizes them.
- Pessimistic versus optimistic concurrency, and when each is wrong.
- Lock ordering and deadlock avoidance.
- Why the side effect (reset email) happens after commit, and what the outbox adds (Slice 6).
- CSRF versus XSS: which storage each attack defeats, and why SameSite plus a custom header is enough here.
- Mass assignment, and why explicit DTOs plus strict binding beat an "ignore" list.
- Open redirects, and how to validate a return URL.
