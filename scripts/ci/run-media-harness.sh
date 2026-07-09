#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
export HARNESS_MODULES="${HARNESS_MODULES:-media}"
exec bash "$ROOT/scripts/ci/run-stack-harness.sh" "$@"
