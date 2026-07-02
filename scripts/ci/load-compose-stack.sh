#!/usr/bin/env bash
# Load harness-stack.tar produced by compose-build-stack.sh.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
LOG_PREFIX="[CI][COMPOSE]"
# shellcheck source=scripts/shared/common.sh
source "$ROOT/scripts/shared/common.sh"

STACK_ARTIFACT="${1:-${STACK_ARTIFACT:-harness-stack.tar}}"
if [[ "$STACK_ARTIFACT" != /* ]]; then
  STACK_ARTIFACT="${ROOT}/${STACK_ARTIFACT}"
fi

[[ -f "$STACK_ARTIFACT" ]] || fail "missing stack artifact: $STACK_ARTIFACT (run compose-build-stack.sh first)"

log_step "LOAD STACK IMAGES FROM ${STACK_ARTIFACT}"
docker load -i "$STACK_ARTIFACT"

log_info "compose stack images loaded"
