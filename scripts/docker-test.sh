#!/usr/bin/env bash
# Integration tests (Testcontainers). Mounts host Docker socket — not for build/EF (see docker-dotnet.sh).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
# shellcheck source=scripts/lib/compose-env.sh
source "$ROOT/scripts/lib/compose-env.sh"

tangle_compose --profile test build test
tangle_compose --profile test run --rm test "$@"
