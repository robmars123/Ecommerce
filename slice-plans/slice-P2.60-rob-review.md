# Slice P2.60 — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: not run (I only run the full gate in a PASS round; yours reported VERIFY PASS)

The conversion is small and reads well: one group, two handlers, one helper, the controller deleted, nothing under Application/ or Infrastructure/. Round-trips are right: `List` makes one `SearchAsync` only after both validation steps, `Get` makes one `GetBySlugAsync`, no `DbContext`/`HttpContext` injected, responses still go through `ToResponse()`. One drift from MVC is left.

### 1. Whitespace-only `page`/`pageSize` is now a 400 — BLOCKING   (`Catalog/Api/ProductEndpoints.cs:61`)
**What I saw:** `ReadInt` treats only `null`/`""` as absent (`string.IsNullOrEmpty`). For `?page=%20`, `int.TryParse(" ")` fails, so you return 400 "The value ' ' is not valid.".
**Why it matters:** MVC's `SimpleTypeModelBinder` treats any `IsNullOrWhiteSpace` value as `null` for non-string types (it skips the converter on purpose), so the controller returned 200 with the defaults. This slice's goal is zero drift, P3 says "MVC treats `page=` as absent", and this case is not in the P6 list of accepted divergences. A client that builds `page=${value}` from a blank field now gets a 400 it never got before.
**What I want:** `string.IsNullOrWhiteSpace(raw)` in `ReadInt`. Extend B1 (or add B1b) with `?page=%20&pageSize=%20` → 200, page 1, pageSize 20.
**How I'll check:** read the one-line diff; run `dotnet test backend/tests/Ecommerce.Api.IntegrationTests -c Release --filter "FullyQualifiedName~ProductQueryBindingTests"`, then the full `verify.sh` in the PASS round.
**Lesson:** "Same as MVC" includes the blank-value rule. Pin the edge before you write the condition, not after.

### Questions you asked
- **Echoing the raw value in the 400 message:** no concern. Kestrel caps the request line (8 KB by default), so the echo is bounded and is the same size MVC already echoed. System.Text.Json escapes it (`<`, `>`, `&`, `'` become `\uXXXX`), and the body is `application/problem+json`, so there is no injection path. Keep the MVC text.
- **Do B1–B5 pin MVC?** Yes for what they cover: empty → default (B1), the exact message (B2), short-circuit before `Create` (B3), surrounding spaces accepted (B4), no `Cache-Control` (B5). Your report says 5/5 were green on MVC before the conversion. The gap is the whitespace-only value above.

**What was good:** P4's short-circuit is kept with a plain early return, and B3 pins it. `ReadInt` is the only helper, and it has a single consumer.

## Round 2 — Rob → Marco
Verdict: PASS · verify.sh: VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)

### 1. Whitespace page value — Confirmed
`ProductEndpoints.cs:61` now uses `string.IsNullOrWhiteSpace`, so `page=%20` falls back to the defaults, matching MVC. B1b (`page=%20&pageSize=%20` gives 200, page 1, pageSize 20) is in `ProductQueryBindingTests.cs`, and your report shows it red with IsNullOrEmpty and green after the fix. I did not re-run mutations because the change is low risk and the full gate passed (jest 2111, build 0 warnings).

**What was good:** B1 and B1b pin both the empty and the whitespace cases, so the MVC-parity behaviour can't regress silently.
