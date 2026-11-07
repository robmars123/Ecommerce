# Local Docker stack — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` — build 0 Warning(s) · jest 45 suites, 280 passed · `verify.sh docker`: `VERIFY PASS (docker)`

The stack matches D1–D15. Every port is on loopback, the postgres service and its volume are untouched, the API environment block uses exactly the planned keys, and `ci.yml` was not edited. No `.env` exists or is tracked; only `.env.example` is untracked, and it has empty values. I re-checked the running containers myself:
- `/`, `/products`, `/products/blue-shirt` and `/admin/products` on :8082 all return 200 with the expo-router root.
- `index.html` is sent with `Cache-Control: no-cache`.
- The bundle contains `localhost:5081` and not `:5080`, so `.env.local` didn't leak into the image.
- :5081 `/health/ready` returns `Healthy [200]`, and `/api/v1/products` returns 200.

Two documentation defects block this round. Each one would mislead the owner.

### 1. `.env.example` describes the JWT key wrongly — BLOCKING   (`.env.example:2`)
**What I saw:** You wrote "Set a key (32+ chars) so sessions survive an api restart."
**Why it matters:** `JwtOptions.TryDecodeKey` expects **base64 of at least 32 bytes**. A 32-character password-like string isn't valid base64, or decodes to only 24 bytes. In Development, `JwtSigningKeyProvider` then falls back to the ephemeral key with nothing but a log warning. Nathaniel would set a key, restart the api, get signed out anyway, and have no visible reason. The comment promises the opposite of what happens.
**What I want:** Change the comment to "base64 of 32+ random bytes". Add the README's existing wording, or a generator one-liner that writes no value into the file, for example `openssl rand -base64 32`.
**How I'll check:** I'll read `.env.example` and compare it with `JwtOptions.MinKeyBytes` and `TryDecodeKey`.
**Lesson:** A config hint must state the format the code validates, not a rough length.

### 2. The README section is above the title and has a corrupt byte — BLOCKING   (`README.md:1-14`)
**What I saw:** "## Local Docker stack" is inserted before `# Ecommerce`, so the file no longer opens with its H1 and the stack summary. Line 9 also contains a lone `0xB7` byte (a Latin-1 middle dot) between the web and api URLs. That is invalid UTF-8 and shows up as `�`.
**Why it matters:** The README's first screen on GitHub and in editors is now a dev-only Docker recipe instead of what the project is. Invalid UTF-8 renders as garbage, and some tools reject the file.
**What I want:** Move the section into "## Local development", after the existing compose/Postgres steps. Keep it as a `### Local Docker stack` subsection with the same content. Replace the byte with a plain `·` written as UTF-8, or with ` / `.
**How I'll check:** `head -3 README.md` shows `# Ecommerce` first. `grep -naxv '.*' README.md` (lines that aren't valid in the current locale) returns nothing, and `file README.md` reports UTF-8.
**Lesson:** Append inside the section a reader would look in; never above a document's title.

### 3. `no-cache` covers every non-bundle file — NOTE   (`frontend/apps/store/nginx.conf`)
`location /` adds `no-cache` to `favicon.png` and other assets as well as `index.html`. D3 asked for it on `index.html`, so this is broader than planned but harmless for a local stack: browsers revalidate those files with a 304. No change needed.

**What was good:** `.env*` in the app's `.dockerignore` keeps `.env.local` (which points at :5080) out of the image; the bundle check proves it. The README's `--connection` flag on `database update` pins the migration to the shared :5434 database whatever the host environment is.

## Round 1 — Marco → Rob

1. **Fixed** — `.env.example:2` now says the key is base64 of 32+ random bytes (JwtOptions.TryDecodeKey) with the hint `openssl rand -base64 32`; no value written.
2. **Fixed** — My Python edit had inserted the section at line 1 (anchor search failed) and wrote a cp1252 0xB7. I moved it under `## Local development` as `### Local Docker stack` (README.md:79) and replaced the byte with ` / `; the file now decodes as UTF-8 and the title is line 1 again. Checked by decoding the bytes and the heading list.

## Round 2 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` — build 0 Warning(s) · jest 45 suites, 280 passed · `verify.sh docker`: `VERIFY PASS (docker)`

Both blocking items are closed.

### 1. JWT key hint — Confirmed
`.env.example:2` now says "Base64 of 32+ random bytes (JwtOptions.TryDecodeKey), e.g. `openssl rand -base64 32`". That matches what the code validates: `openssl rand -base64 32` produces exactly 32 bytes, base64-encoded. The value lines are still empty, and `git status` shows only the untracked `.env.example`, with no `.env`.

### 2. README placement and encoding — Confirmed
`head -3 README.md` starts with `# Ecommerce`. The section is now `### Local Docker stack` at line 79, inside `## Local development` (line 39). `grep -naxv '.*' README.md` returns nothing, there are 0 `0xB7` bytes, and `file` reports UTF-8. The comment now reads `web http://localhost:8082  / api http://localhost:5081`. The working copy has CRLF line endings, but `git diff --stat` shows only your 25 insertions and 2 deletions, so git normalises them and there is no whole-file churn.

**Still owed (not blocking):** Nathaniel's sign-in check in a browser at :8082: log in, reload, and still be signed in. Leave the containers running for him.

**What was good:** The key hint names the function that validates it, so the next reader can check the rule against the code instead of trusting the comment.
