#!/usr/bin/env bash
# Load harness-stack.tar produced by compose-build-stack.sh.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
LOG_PREFIX="[CI][COMPOSE]"
# shellcheck source=scripts/shared/common.sh
source "$ROOT/scripts/shared/common.sh"
# shellcheck source=scripts/ci/libs/versions-prod-env.sh
source "$ROOT/scripts/ci/libs/versions-prod-env.sh"
load_versions_prod_env "$ROOT"

STACK_ARTIFACT="${1:-${STACK_ARTIFACT:-harness-stack.tar}}"
if [[ "$STACK_ARTIFACT" != /* ]]; then
  STACK_ARTIFACT="${ROOT}/${STACK_ARTIFACT}"
fi

SDK_IMAGE="${SDK_IMAGE:-tangle-study-sdk:local}"
WORKER_MEDIA_IMAGE="${WORKER_MEDIA_IMAGE:-tangle-study-worker-media:local}"
COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-tangle-study}"

EXPECTED_IMAGES=(
  "${COMPOSE_PROJECT_NAME}-api"
  "${COMPOSE_PROJECT_NAME}-media"
  "${COMPOSE_PROJECT_NAME}-nginx"
  "$WORKER_MEDIA_IMAGE"
  "$SDK_IMAGE"
)

[[ -f "$STACK_ARTIFACT" ]] || fail "missing stack artifact: $STACK_ARTIFACT (run compose-build-stack.sh first)"

log_step "LOAD STACK IMAGES FROM ${STACK_ARTIFACT}"
docker load -i "$STACK_ARTIFACT"

for image in "${EXPECTED_IMAGES[@]}"; do
  docker image inspect "$image" >/dev/null 2>&1 \
    || fail "stack artifact did not provide image: $image"
done

log_info "compose stack images loaded"
