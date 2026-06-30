#!/usr/bin/env bash
set -euo pipefail

: "${AZURE_RESOURCE_GROUP:?required}"
: "${CONTAINER_REGISTRY:?required}"
: "${IMAGE_TAG:?required}"

echo "========================================"
echo "[DEPLOY][ACA] PHASE 3 - UPDATE CONTAINER APPS"
echo "========================================"

update() {
  local app="$1"
  local image="$2"
  local ref="${CONTAINER_REGISTRY}/${image}:${IMAGE_TAG}"

  echo "==> UPDATE ${app} -> ${ref}"

  az containerapp update \
    --name "$app" \
    --resource-group "$AZURE_RESOURCE_GROUP" \
    --image "$ref" \
    --output none
}

update_worker() {
  local app="$1"
  local role="$2"

  echo "==> updating $app with role=$role -> ${ref}"

  az containerapp update \
    --name "$app" \
    --resource-group "$AZURE_RESOURCE_GROUP" \
    --image "${CONTAINER_REGISTRY}/tangle-study-worker:${IMAGE_TAG}" \
    --set-env-vars WORKER_TYPE="$role"
}

# CORE
update tangle-study-api tangle-study-api
update tangle-study-web tangle-study-web

# WORKERS
update_worker tangle-study-worker-chat chat
update_worker tangle-study-worker-location location
update_worker tangle-study-worker-media media

echo "========================================"
echo "[DEPLOY][ACA] DONE"
echo "========================================"