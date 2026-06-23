#!/usr/bin/env bash
# Update Container App / Job images in Azure after GHCR push.
#
# Required env:
#   AZURE_RESOURCE_GROUP
#   CONTAINER_REGISTRY   e.g. ghcr.io/my-org/tangle-study
#   IMAGE_TAG            e.g. git SHA
#
set -euo pipefail

: "${AZURE_RESOURCE_GROUP:?AZURE_RESOURCE_GROUP is required}"
: "${CONTAINER_REGISTRY:?CONTAINER_REGISTRY is required}"
: "${IMAGE_TAG:?IMAGE_TAG is required}"

update_app() {
  local name="$1"
  local image_name="$2"
  echo "==> Updating $name -> ${CONTAINER_REGISTRY}/${image_name}:${IMAGE_TAG}"
  az containerapp update \
    --name "$name" \
    --resource-group "$AZURE_RESOURCE_GROUP" \
    --image "${CONTAINER_REGISTRY}/${image_name}:${IMAGE_TAG}" \
    --output none
}

update_app "tangle-study-api" "tangle-study-api"
update_app "tangle-study-web" "tangle-study-web"
update_app "tangle-study-worker-chat" "tangle-study-worker"
update_app "tangle-study-worker-media" "tangle-study-worker"
update_app "tangle-study-worker-location" "tangle-study-worker"

echo "==> Updating migrate job image"
az containerapp job update \
  --name "tangle-study-migrate" \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --image "${CONTAINER_REGISTRY}/tangle-study-api:${IMAGE_TAG}" \
  --output none

echo "==> Container app images updated."
