# Slice P2.61 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)`

A clean, literal port. The handler is 20 lines, reads top to bottom, and does exactly what the controller did plus the one explicit 400 that Amendment 1 asked for.

What I checked against plan §5 and Amendment 1:
- Route `api/v1/images/{**key}`, no group, `.AllowAnonymous()`; inventory and II11 pass unedited.
- `string? key`; the `IsNullOrEmpty` branch runs before the service call and before any header is set. Its body matches your MVC dump field for field (type 15.5.1, title, status, `errors.key`, traceId, correlationId), and the theory asserts all of it.
- One `images.GetAsync` call; no `IImageStorage`/`DbContext`; the 304 path uses the same row's ETag and `lastModified` through `FileContentHttpResult`, so there is no second round-trip. Storage is still one PK query that selects 4 columns. No N+1, no over-fetch.
- `Cache-Control` and `nosniff` are set only after the null check, with the same literals; no range processing and no `fileDownloadName`. IF1 (headers) and IF2 (304, empty body) pass unedited.
- Test diff is one appended hunk after IF3; IF1–IF3 are byte-identical.
- Step-4 red quoted (BadRequest expected, NotFound actual), restored, `cmp` OK.

### 1. Hygiene awk abort on the deleted controller — NOTE
**What I saw:** in the hygiene line, `awk: fatal: cannot open file ...ImagesController.cs`, but the step still passed.
**Why it matters:** awk stops at the first missing file, so the `var` scan skips the files after it. This slice is still safe: IDE0008 is a build error and I grepped `ImageEndpoints.cs` by hand (no hits). The cause is in the script, not in your diff; it is already in the backlog (P2.56/P2.59), now marked as seen again.
**Lesson:** a gate that prints an error and still passes is a gate you can't trust; fix it once, at the root.

**What was good:** you split the theory when the MVC baseline proved my premise wrong, and you pinned the dump before converting instead of fitting the assertion to the new code. The one-line comment on `string? key` says why it is nullable, which is the one thing a reader would ask.
