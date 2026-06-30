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

# CORE
update tangle-study-api tangle-study-api
update tangle-study-web tangle-study-web

# WORKERS
update tangle-study-worker-chat tangle-study-worker-chat
update tangle-study-worker-location tangle-study-worker-location
update tangle-study-worker-media tangle-study-worker-media

# INFRA
update tangle-study-redis tangle-study-redis

# OBSERVABILITY
update tangle-study-prometheus tangle-study-prometheus
update tangle-study-grafana tangle-study-grafana
update tangle-study-postgres-exporter tangle-study-postgres-exporter

echo "========================================"
echo "[DEPLOY][ACA] DONE"
echo "========================================"