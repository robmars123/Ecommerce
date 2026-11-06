#!/usr/bin/env bash
# Local mirror of CI (.github/workflows/ci.yml) and quality-bar §4.
# One line per step; full logs in artifacts/verify/<step>.log; a failing step prints its last 40 lines.
# Usage: bash scripts/verify.sh [step ...] | changed
# Steps (default = all except docker): secrets hygiene build test drift expo-check tsc jest export docker
# "changed" picks steps from the working-tree diff (inner loop). The full run is the gate before any report.
# Env: VERIFY_CONFIGURATION (default Release; Release avoids a Debug build locked by a running debugger).
set -uo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
app="$root/frontend/apps/store"
logs="$root/artifacts/verify"
cfg="${VERIFY_CONFIGURATION:-Release}"
export NO_COLOR=1 FORCE_COLOR=0 DOTNET_NOLOGO=1 CI=1
mkdir -p "$logs"
cd "$root" || exit 2

g() { git -c safe.directory='*' "$@"; }
# Tracked + untracked, never ignored — CI scans tracked files only, but here most work is uncommitted.
gg() { g grep --untracked -nIE "$@"; }

step_secrets() {
  local p='sk_(live|test)_[A-Za-z0-9]{10,}|rk_(live|test)_[A-Za-z0-9]{10,}|whsec_[A-Za-z0-9]{10,}|-----BEGIN ([A-Z]+ )?PRIVATE KEY-----|[Pp]assword=[^<;"[:space:]]'
  if gg "$p" -- ':!scripts/verify.sh' ':!.github/workflows/ci.yml'; then echo "Possible secret (matches above)"; return 1; fi
  echo "no matches"
}
# Code lines in backend .cs that use `var` (IDE0008 skips anonymous types). Comment lines are skipped; strings and trailing comments stripped first.
cs_var_hits() {
  g ls-files --cached --others --exclude-standard -- 'backend/*.cs' | grep -vE '/(bin|obj)/' | xargs awk '
    /^[[:space:]]*(\/\/|\/\*|\*)/ { next }
    { s = $0; gsub(/[@$]*"([^"\\]|\\.)*"/, "\"\"", s); sub(/\/\/.*/, "", s) }
    s ~ /(^|[^[:alnum:]_.@])var([[:space:]]+[@_[:alpha:]]|[[:space:]]*\()/ { print FILENAME ":" FNR ": " $0 }'
}

step_hygiene() {
  if g ls-files --cached --others --exclude-standard -- frontend/apps/store/app | grep -E '\.test\.(ts|tsx)$'; then
    echo "Test files under app/ become routes (architecture §5.4)"; return 1
  fi
  if gg '(^|[^[:alnum:]_.])((it|test|describe)\.only|fit|fdescribe)[(.]' -- '*.test.ts' '*.test.tsx'; then
    echo "Focused test found"; return 1
  fi
  if cs_var_hits | grep .; then
    echo "C# var found (explicit types only)"; return 1
  fi
  echo "no matches"
}
step_build()      { dotnet build Ecommerce.slnx -c "$cfg" --no-incremental; }
step_test()       { dotnet test Ecommerce.slnx -c "$cfg"; }
step_drift()      { dotnet tool restore >/dev/null && dotnet tool run dotnet-ef migrations has-pending-model-changes \
                      --project backend/src/Ecommerce.Persistence --startup-project backend/src/Ecommerce.Api --configuration "$cfg"; }
step_expo-check() { cd "$app" && npx expo install --check; }
step_tsc()        { cd "$app" && npx tsc --noEmit && echo "tsc clean"; }
step_jest()       { cd "$app" && npm test -- --ci; }
step_export()     { cd "$app" && npx expo export --platform web; }
step_docker()     { docker build -t ecommerce-api:local . && docker build -t ecommerce-web:local frontend/apps/store; }

# Summary lines kept from each log (everything else stays in the log file).
summary_of() {
  case $1 in
    build)  echo 'Warning\(s\)|Error\(s\)|warning CS|error CS|Build succeeded|Build failed' ;;
    test)   echo '(Passed|Failed)!|Total tests|Test summary|total: ' ;;
    drift)  echo 'changes' ;;
    jest)   echo '^(Tests|Test Suites):' ;;
    export) echo 'Exported|App exported' ;;
    expo-check) echo 'up to date|should be updated|expected version' ;;
    *)      echo '.' ;;
  esac
}

# "changed": only the steps the working-tree changes can affect (fast inner loop; the full run is the gate).
changed_steps() {
  local files; files=$(g status --porcelain --untracked-files=all | sed 's/^...//; s/.* -> //')
  local s=(secrets hygiene)
  if grep -qE '^(backend/|Directory\.|Ecommerce\.slnx|global\.json|\.config/)' <<<"$files"; then s+=(build test); fi
  if grep -qE '^backend/src/(Ecommerce\.Persistence|Modules/[^/]+/[^/]+/Infrastructure/Persistence)/' <<<"$files"; then s+=(drift); fi
  if grep -qE '^frontend/apps/store/package(-lock)?\.json' <<<"$files"; then s+=(expo-check); fi
  if grep -qE '^frontend/' <<<"$files"; then s+=(tsc jest export); fi
  echo "${s[@]}"
}

steps=("$@")
[ ${#steps[@]} -eq 0 ] && steps=(secrets hygiene build test drift expo-check tsc jest export)
if [ "${steps[*]}" = "changed" ]; then read -ra steps <<<"$(changed_steps)"; echo "changed → ${steps[*]}"; fi

failed=()
for s in "${steps[@]}"; do
  if ! declare -F "step_$s" >/dev/null; then echo "unknown step: $s"; exit 2; fi
  log="$logs/$s.log"; start=$SECONDS
  ( "step_$s" ) >"$log" 2>&1; rc=$?
  sum=$(sed 's/\x1b\[[0-9;]*[A-Za-z]//g' "$log" | grep -E "$(summary_of "$s")" | sed 's/^[[:space:]]*//' | sort -u | tail -n 8 | paste -sd'|' -)
  if [ $rc -eq 0 ]; then
    printf 'PASS  %-10s %4ss  %s\n' "$s" $((SECONDS - start)) "${sum:0:300}"
  else
    failed+=("$s")
    printf 'FAIL  %-10s %4ss  exit %s  log: artifacts/verify/%s.log\n' "$s" $((SECONDS - start)) "$rc" "$s"
    sed 's/\x1b\[[0-9;]*[A-Za-z]//g' "$log" | tail -n 40 | sed 's/^/      /'
  fi
done

if [ ${#failed[@]} -eq 0 ]; then echo "VERIFY PASS (${steps[*]})"; else echo "VERIFY FAIL: ${failed[*]}"; exit 1; fi
