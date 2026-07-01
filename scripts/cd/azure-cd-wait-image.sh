#!/usr/bin/env bash
set -euo pipefail

: "${CONTAINER_REGISTRY:?required}"
: "${IMAGE_TAG:?required}"

echo "========================================"
echo "[DEPLOY][IMAGE] PHASE 2 - GHCR CONSISTENCY GATE (OPTIMIZED)"
echo "========================================"

IMAGES=(
  tangle-study-api
  tangle-study-web
  tangle-study-worker
  tangle-study-prometheus
  tangle-study-grafana
)

wait_image() {
  local image="$1"
  local ref="${CONTAINER_REGISTRY}/${image}:${IMAGE_TAG}"

  echo "==> CHECK ${ref}"

  for i in {1..40}; do
    if docker buildx imagetools inspect "$ref" >/dev/null 2>&1; then
      echo "==> READY ${image}"
      return 0
    fi

    echo "    not ready (${i}/40)"
    sleep 5
  done

  echo "[FATAL] GHCR manifest not available: ${ref}" >&2
  return 1
}

fail=0

for img in "${IMAGES[@]}"; do
  if ! wait_image "$img"; then
    fail=1
  fi
done

[[ $fail -eq 0 ]] || exit 1

echo "========================================"
echo "[DEPLOY][IMAGE] ALL READY"
echo "========================================"