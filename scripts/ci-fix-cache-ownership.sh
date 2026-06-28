#!/usr/bin/env bash
# Fix ownership on CI cache dirs after Docker (root) writes. Safe on host; no-op when dirs missing.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=scripts/lib/ci-cache.sh
source "$ROOT/scripts/lib/ci-cache.sh"

ci_fix_cache_ownership
