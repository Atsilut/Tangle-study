#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
LOG_PREFIX="[DEPLOY][IMAGE]"
# shellcheck source=scripts/shared/common.sh
source "$ROOT/scripts/shared/common.sh"

require_env CONTAINER_REGISTRY
require_env IMAGE_TAG

log_step "GHCR CONSISTENCY GATE"

IMAGES=(
  tangle-study-web
  tangle-study-worker-media
  tangle-study-worker-chat
  tangle-study-worker-location
  tangle-study-prometheus
  tangle-study-grafana
)

wait_image() {
  local image="$1"
  local ref="${CONTAINER_REGISTRY}/${image}:${IMAGE_TAG}"

  log_info "check ${ref}"

  for i in {1..40}; do
    if docker buildx imagetools inspect "$ref" >/dev/null 2>&1; then
      log_info "ready ${image}"
      return 0
    fi

    log_info "not ready (${i}/40)"
    sleep 5
  done

  log_error "GHCR manifest not available: ${ref}"
  return 1
}

gate_failed=0

for img in "${IMAGES[@]}"; do
  if ! wait_image "$img"; then
    gate_failed=1
  fi
done

[[ $gate_failed -eq 0 ]] || fail "one or more GHCR manifests not available"

log_step "ALL IMAGES READY"
