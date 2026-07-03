#!/usr/bin/env bash
# Build harness stack images from precompiled artifacts; save as harness-stack.tar for CI.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
LOG_PREFIX="[CI][COMPOSE]"
# shellcheck source=scripts/shared/common.sh
source "$ROOT/scripts/shared/common.sh"
# shellcheck source=scripts/shared/compose-env.sh
source "$ROOT/scripts/shared/compose-env.sh"
# shellcheck source=scripts/ci/libs/versions-prod-env.sh
source "$ROOT/scripts/ci/libs/versions-prod-env.sh"
load_versions_prod_env "$ROOT"

STACK_ARTIFACT="${STACK_ARTIFACT:-harness-stack.tar}"
SDK_IMAGE="${SDK_IMAGE:-tangle-study-sdk:local}"
WORKER_MEDIA_IMAGE="${WORKER_MEDIA_IMAGE:-tangle-study-worker-media:local}"
COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-tangle-study}"

COMPOSE_FILES=(
  -f docker-compose.yml
  -f docker-compose.harness.yml
  -f docker-compose.runtime.yml
  --profile harness
)

require_publish_output() {
  local dir="$1"
  local dll="$2"
  [[ -f "${ROOT}/${dir}/${dll}" ]] || fail "missing ${dir}/${dll} — run ./scripts/ci/dotnet-publish.sh first"
}

require_release_binary() {
  local path="$1"
  [[ -f "${ROOT}/${path}" ]] || fail "missing ${path} — run ./scripts/ci/build-workers-release.sh first"
}

require_publish_output ".ci-cache/publish/api" "Api.dll"
require_publish_output ".ci-cache/publish/media" "Media.dll"
require_release_binary "workers/target/release/worker-media"

log_step "BUILD WORKER RUNTIME IMAGE"
WORKER_MEDIA_IMAGE="$WORKER_MEDIA_IMAGE" bash "$ROOT/scripts/ci/build-worker-images.sh" --media-only

log_step "BUILD STACK SERVICE IMAGES"
tangle_compose "${COMPOSE_FILES[@]}" build --parallel api media nginx rust-worker-media harness

STACK_IMAGES=(
  "${COMPOSE_PROJECT_NAME}-api"
  "${COMPOSE_PROJECT_NAME}-media"
  "${COMPOSE_PROJECT_NAME}-nginx"
  "$WORKER_MEDIA_IMAGE"
  "$SDK_IMAGE"
)

for image in "${STACK_IMAGES[@]}"; do
  docker image inspect "$image" >/dev/null 2>&1 || fail "missing stack image: $image (compose build failed?)"
done

log_step "SAVE STACK IMAGES TO ${STACK_ARTIFACT}"
docker save -o "${ROOT}/${STACK_ARTIFACT}" "${STACK_IMAGES[@]}"

log_info "compose stack artifact written: ${STACK_ARTIFACT}"
