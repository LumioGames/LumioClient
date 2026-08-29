#!/usr/bin/env bash
# Hard gate: the vendored mirror still holds the bytes the lock recorded.
# Self-contained on purpose — it never reads LumioGameEngineArchitecture, so it
# is a real gate on CI where no sibling checkout exists. Upstream drift is a
# separate, report-only question answered by sync-contract-mirror.sh --check.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

MIRROR_DIR="contract-mirror/upstream"
LOCK="contract-mirror/contract-mirror.sha256"

EXIT_TAMPERED=33
EXIT_USAGE=2

sha256_of() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | cut -d' ' -f1
  else
    shasum -a 256 "$1" | cut -d' ' -f1
  fi
}

if [ ! -f "$LOCK" ]; then
  echo "contract-mirror: lock file missing: $LOCK" >&2
  exit "$EXIT_USAGE"
fi

if [ ! -d "$MIRROR_DIR" ]; then
  echo "contract-mirror: mirror directory missing: $MIRROR_DIR" >&2
  exit "$EXIT_USAGE"
fi

failures=0

# 1. every locked path must exist and still hash to the locked value
while IFS= read -r line; do
  [ -z "$line" ] && continue
  expected="${line%% *}"
  path="${line#* }"
  path="${path# }"

  if [ ! -f "$path" ]; then
    echo "contract-mirror: MISSING $path" >&2
    failures=$((failures + 1))
    continue
  fi

  actual="$(sha256_of "$path")"
  if [ "$actual" != "$expected" ]; then
    echo "contract-mirror: MODIFIED $path" >&2
    echo "  expected $expected" >&2
    echo "  actual   $actual" >&2
    failures=$((failures + 1))
  fi
done < "$LOCK"

# 2. no unlocked file may hide inside the mirror
locked_paths="$(mktemp)"
actual_paths="$(mktemp)"
trap 'rm -f "$locked_paths" "$actual_paths"' EXIT

cut -d' ' -f3- "$LOCK" | sed '/^$/d' | LC_ALL=C sort > "$locked_paths"
find "$MIRROR_DIR" -type f | LC_ALL=C sort > "$actual_paths"

while IFS= read -r path; do
  [ -z "$path" ] && continue
  echo "contract-mirror: UNLOCKED $path" >&2
  failures=$((failures + 1))
done < <(LC_ALL=C comm -13 "$locked_paths" "$actual_paths")

if [ "$failures" -ne 0 ]; then
  echo "contract-mirror: FAILED — $failures file(s) diverge from $LOCK" >&2
  echo "contract-mirror: the mirror is read-only; restore it with eng/sync-contract-mirror.sh" >&2
  exit "$EXIT_TAMPERED"
fi

echo "contract-mirror: OK — every mirrored file matches $LOCK"
