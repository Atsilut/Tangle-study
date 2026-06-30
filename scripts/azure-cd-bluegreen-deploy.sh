#!/usr/bin/env bash
# Blue-Green deployment for Azure Container Apps (safe traffic switch)

set -euo pipefail

: "${AZURE_RESOURCE_GROUP:?AZURE_RESOURCE_GROUP is required}"
: "${WEB_APP_NAME:=tangle-study-web}"
: "${API_APP_NAME:=tangle-study-api}"

RG="$AZURE_RESOURCE_GROUP"

wait_healthy() {
  local app="$1"

  echo "==> Waiting healthy revision: $app"

  for i in {1..60}; do
    status="$(az containerapp revision list \
      --name "$app" \
      --resource-group "$RG" \
      --query "[?properties.active==\`true\`].properties.healthState" \
      -o tsv | head -n1)"

    if [[ "$status" == "Healthy" ]]; then
      echo "[OK] $app healthy"
      return 0
    fi

    sleep 5
  done

  echo "[FATAL] $app did not become healthy" >&2
  exit 1
}

switch_traffic() {
  local app="$1"
  local revision="$2"

  echo "==> Switching traffic: $app -> $revision"

  az containerapp ingress traffic set \
    --name "$app" \
    --resource-group "$RG" \
    --revision-weight "$revision=100" \
    --output none

  echo "[OK] Traffic switched"
}

deploy_app() {
  local app="$1"
  local image="$2"

  echo "========================================"
  echo "[BLUE-GREEN] DEPLOY $app"
  echo "========================================"

  # create new revision (no traffic)
  az containerapp update \
    --name "$app" \
    --resource-group "$RG" \
    --image "$image" \
    --revision-suffix "bg-$(date +%s)" \
    --no-wait \
    --output none

  wait_healthy "$app"

  local latest_revision
  latest_revision="$(az containerapp revision list \
    --name "$app" \
    --resource-group "$RG" \
    --query "sort_by(@,&properties.createdTime)[-1].name" \
    -o tsv)"

  switch_traffic "$app" "$latest_revision"
}

deploy_app "$API_APP_NAME"  "ghcr.io/atsilut/tangle-study/tangle-study-api:${IMAGE_TAG}"
deploy_app "$WEB_APP_NAME"  "ghcr.io/atsilut/tangle-study/tangle-study-web:${IMAGE_TAG}"

echo "========================================"
echo "[BLUE-GREEN] DEPLOY COMPLETE"
echo "========================================"