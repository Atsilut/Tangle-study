#!/usr/bin/env bash
# Inject GitHub Environment secrets into Azure Container Apps.
#
# Required env:
#   AZURE_RESOURCE_GROUP
#   BLOB_CONNECTION_STRING
#   JWT_SECRET
#   WORKER_CALLBACK_SECRET
#   METRICS_SCRAPE_SECRET
#
# Optional env:
#   PLACES_API_KEY
#
set -euo pipefail

: "${AZURE_RESOURCE_GROUP:?AZURE_RESOURCE_GROUP is required}"
: "${BLOB_CONNECTION_STRING:?BLOB_CONNECTION_STRING is required}"
: "${JWT_SECRET:?JWT_SECRET is required}"
: "${WORKER_CALLBACK_SECRET:?WORKER_CALLBACK_SECRET is required}"
: "${METRICS_SCRAPE_SECRET:?METRICS_SCRAPE_SECRET is required}"

RG="$AZURE_RESOURCE_GROUP"
PLACES_API_KEY="${PLACES_API_KEY:-}"

echo "==> API secrets (tangle-api)"
SECRET_ARGS=(
  "blob-conn=${BLOB_CONNECTION_STRING}"
  "jwt-secret=${JWT_SECRET}"
  "worker-callback=${WORKER_CALLBACK_SECRET}"
  "metrics-secret=${METRICS_SCRAPE_SECRET}"
)
if [[ -n "$PLACES_API_KEY" ]]; then
  SECRET_ARGS+=("places-api-key=${PLACES_API_KEY}")
fi

az containerapp secret set \
  --name tangle-api \
  --resource-group "$RG" \
  --secrets "${SECRET_ARGS[@]}" \
  --output none

API_ENV=(
  "Media__ConnectionString=secretref:blob-conn"
  "Jwt__Secret=secretref:jwt-secret"
  "Media__WorkerCallbackSecret=secretref:worker-callback"
  "Metrics__ScrapeSecret=secretref:metrics-secret"
)
if [[ -n "$PLACES_API_KEY" ]]; then
  API_ENV+=("Places__ApiKey=secretref:places-api-key")
fi

az containerapp update \
  --name tangle-api \
  --resource-group "$RG" \
  --set-env-vars "${API_ENV[@]}" \
  --output none

echo "==> Media worker secrets (tangle-worker-media)"
az containerapp secret set \
  --name tangle-worker-media \
  --resource-group "$RG" \
  --secrets \
    "blob-conn=${BLOB_CONNECTION_STRING}" \
    "worker-callback=${WORKER_CALLBACK_SECRET}" \
  --output none

az containerapp update \
  --name tangle-worker-media \
  --resource-group "$RG" \
  --set-env-vars \
    "AZURE_STORAGE_CONNECTION_STRING=secretref:blob-conn" \
    "WORKER_CALLBACK_SECRET=secretref:worker-callback" \
  --output none

echo "==> Secrets injected."
