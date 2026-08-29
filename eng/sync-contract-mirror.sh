#!/usr/bin/env bash
# Re-vendor the architecture source's published surface, or report drift against it.
#
# This needs a LumioGameEngineArchitecture checkout and therefore CANNOT be a CI
# gate — CI has no sibling repo. The gate is verify-contract-mirror.sh, which is
# self-contained. Keep the two questions apart:
#   "were the vendored bytes hand-edited?"  -> verify-contract-mirror.sh (hard)
#   "has upstream moved past our pin?"      -> this script, --check (report)
#
# Only committed objects are read (git archive <commit>); the source working
# tree is never consulted, so an unrelated dirty checkout cannot leak in.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

MIRROR_DIR="contract-mirror/upstream"
LOCK="contract-mirror/contract-mirror.sha256"
PIN_FILE="contract-mirror/MIRROR.md"

# The mirror scope is a rule, not a hand-kept file list: these upstream
# directories are vendored whole, with no exceptions. An exception list is a
# second thing to keep correct, and packages/index.json — the registry that
# records every artifact's compilerHash and baselineId — sits at the packages
# root, so any sub-selection would have had to carve around it.
MIRROR_SCOPE=(schemas fixtures ids packages)

SOURCE=""
COMMIT=""
CHECK_ONLY=0

usage() {
  cat >&2 <<'USAGE'
usage: eng/sync-contract-mirror.sh --source <path-to-LumioGameEngineArchitecture> [--commit <sha>] [--check]

  --source   required; path to a LumioGameEngineArchitecture checkout.
             Deliberately not read from the environment: a gate that silently
             passes when the variable is unset is not a gate.
  --commit   upstream commit to pin (default: the pin recorded in MIRROR.md).
  --check    report drift only; write nothing and always exit 0.
USAGE
}

while [ $# -gt 0 ]; do
  case "$1" in
    --source) SOURCE="${2:-}"; shift 2 ;;
    --commit) COMMIT="${2:-}"; shift 2 ;;
    --check)  CHECK_ONLY=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "unknown argument: $1" >&2; usage; exit 2 ;;
  esac
done

if [ -z "$SOURCE" ]; then
  echo "contract-mirror: --source is required" >&2
  usage
  exit 2
fi

if [ ! -d "$SOURCE/.git" ]; then
  echo "contract-mirror: not a git checkout: $SOURCE" >&2
  exit 2
fi

if [ -z "$COMMIT" ]; then
  if [ -f "$PIN_FILE" ]; then
    COMMIT="$(grep -oE '^- upstream commit: `[0-9a-f]{40}`' "$PIN_FILE" | grep -oE '[0-9a-f]{40}' || true)"
  fi
fi

if [ -z "$COMMIT" ]; then
  echo "contract-mirror: no --commit given and no pin found in $PIN_FILE" >&2
  exit 2
fi

if ! git -C "$SOURCE" cat-file -e "${COMMIT}^{commit}" 2>/dev/null; then
  echo "contract-mirror: commit not found in $SOURCE: $COMMIT" >&2
  exit 2
fi

STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

git -C "$SOURCE" archive --format=tar "$COMMIT" -- "${MIRROR_SCOPE[@]}" | tar -x -C "$STAGE"

if [ "$CHECK_ONLY" -eq 1 ]; then
  drift=0
  while IFS= read -r rel; do
    src="$STAGE/$rel"
    dst="$MIRROR_DIR/$rel"
    if [ ! -f "$dst" ]; then
      echo "drift: ABSENT-HERE   $rel"
      drift=$((drift + 1))
    elif ! cmp -s "$src" "$dst"; then
      echo "drift: BYTES-DIFFER  $rel"
      drift=$((drift + 1))
    fi
  done < <(cd "$STAGE" && find . -type f | sed 's|^\./||' | LC_ALL=C sort)

  while IFS= read -r rel; do
    if [ ! -f "$STAGE/$rel" ]; then
      echo "drift: GONE-UPSTREAM $rel"
      drift=$((drift + 1))
    fi
  done < <(cd "$MIRROR_DIR" 2>/dev/null && find . -type f | sed 's|^\./||' | LC_ALL=C sort || true)

  if [ "$drift" -eq 0 ]; then
    echo "contract-mirror: in sync with $SOURCE at $COMMIT"
  else
    echo "contract-mirror: $drift file(s) drift from $SOURCE at $COMMIT — re-run without --check to re-vendor"
  fi
  # Report-only by contract: drift is news, not a build break.
  exit 0
fi

rm -rf "$MIRROR_DIR"
mkdir -p "$MIRROR_DIR"
(cd "$STAGE" && tar -cf - .) | (cd "$MIRROR_DIR" && tar -xf -)
find "$MIRROR_DIR" -type f -exec chmod 0444 {} +

sha256_of() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | cut -d' ' -f1
  else
    shasum -a 256 "$1" | cut -d' ' -f1
  fi
}

: > "$LOCK"
while IFS= read -r path; do
  printf '%s  %s\n' "$(sha256_of "$path")" "$path" >> "$LOCK"
done < <(find "$MIRROR_DIR" -type f | LC_ALL=C sort)

echo "contract-mirror: vendored $(wc -l < "$LOCK" | tr -d ' ') file(s) from $SOURCE at $COMMIT"
echo "contract-mirror: remember to update the pin in $PIN_FILE to $COMMIT"
