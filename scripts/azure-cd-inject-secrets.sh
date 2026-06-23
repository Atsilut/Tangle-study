#!/usr/bin/env bash
# Inject GitHub Environment secrets into Azure Container Apps.
#
# Required env:
#   AZURE_RESOURCE_GROUP
#   BLOB_CONNECTION_STRING
#   JWT_SECRET
#   WORKER_CALLBACK_SECRET
#   METRICS_SCRAPE_SECRET
#   POSTGRES_ADMIN_PASSWORD
#
# Optional env:
#   PLACES_API_KEY
#   POSTGRES_ADMIN_LOGIN (default: tangle)
#   APPLICATIONINSIGHTS_CONNECTION_STRING (auto-fetched from Azure when unset)
#   APP_INSIGHTS_NAME (default: tanglestudyprod-appi)
#   GHCR_REGISTRY_USERNAME / GHCR_REGISTRY_PASSWORD (private GHCR pull)
#
set -euo pipefail

: "${AZURE_RESOURCE_GROUP:?AZURE_RESOURCE_GROUP is required}"
: "${BLOB_CONNECTION_STRING:?BLOB_CONNECTION_STRING is required}"
: "${JWT_SECRET:?JWT_SECRET is required}"
: "${WORKER_CALLBACK_SECRET:?WORKER_CALLBACK_SECRET is required}"
: "${METRICS_SCRAPE_SECRET:?METRICS_SCRAPE_SECRET is required}"
: "${POSTGRES_ADMIN_PASSWORD:?POSTGRES_ADMIN_PASSWORD is required}"

RG="$AZURE_RESOURCE_GROUP"
PLACES_API_KEY="${PLACES_API_KEY:-}"
POSTGRES_ADMIN_LOGIN="${POSTGRES_ADMIN_LOGIN:-tangle}"
APP_INSIGHTS_NAME="${APP_INSIGHTS_NAME:-tanglestudyprod-appi}"

POSTGRES_CONNECTION_STRING="Host=tangle-study-postgres;Port=5432;Database=tangledb;Username=${POSTGRES_ADMIN_LOGIN};Password=${POSTGRES_ADMIN_PASSWORD};Pooling=true"

if [[ -z "${APPLICATIONINSIGHTS_CONNECTION_STRING:-}" ]]; then
  echo "==> Resolving Application Insights connection string from Azure (${APP_INSIGHTS_NAME})"
  APPLICATIONINSIGHTS_CONNECTION_STRING="$(az monitor app-insights component show \
    --app "$APP_INSIGHTS_NAME" \
    --resource-group "$RG" \
    --query connectionString \
    --output tsv)"
fi
: "${APPLICATIONINSIGHTS_CONNECTION_STRING:?APPLICATIONINSIGHTS_CONNECTION_STRING is required (set secret or ensure App Insights exists)}"

echo "==> Postgres container (tangle-study-postgres)"
az containerapp secret set \
  --name tangle-study-postgres \
  --resource-group "$RG" \
  --secrets "postgres-password=${POSTGRES_ADMIN_PASSWORD}" \
  --output none

az containerapp update \
  --name tangle-study-postgres \
  --resource-group "$RG" \
  --set-env-vars "POSTGRES_PASSWORD=secretref:postgres-password" \
  --output none

echo "==> API secrets (tangle-study-api)"
SECRET_ARGS=(
  "postgres-conn=${POSTGRES_CONNECTION_STRING}"
  "blob-conn=${BLOB_CONNECTION_STRING}"
  "jwt-secret=${JWT_SECRET}"
  "worker-callback=${WORKER_CALLBACK_SECRET}"
  "metrics-secret=${METRICS_SCRAPE_SECRET}"
  "appinsights-conn=${APPLICATIONINSIGHTS_CONNECTION_STRING}"
)
if [[ -n "$PLACES_API_KEY" ]]; then
  SECRET_ARGS+=("places-api-key=${PLACES_API_KEY}")
fi

az containerapp secret set \
  --name tangle-study-api \
  --resource-group "$RG" \
  --secrets "${SECRET_ARGS[@]}" \
  --output none

API_ENV=(
  "ConnectionStrings__DefaultConnection=secretref:postgres-conn"
  "Media__ConnectionString=secretref:blob-conn"
  "Jwt__Secret=secretref:jwt-secret"
  "Media__WorkerCallbackSecret=secretref:worker-callback"
  "Metrics__ScrapeSecret=secretref:metrics-secret"
  "APPLICATIONINSIGHTS_CONNECTION_STRING=secretref:appinsights-conn"
)
if [[ -n "$PLACES_API_KEY" ]]; then
  API_ENV+=("Places__ApiKey=secretref:places-api-key")
fi

az containerapp update \
  --name tangle-study-api \
  --resource-group "$RG" \
  --set-env-vars "${API_ENV[@]}" \
  --output none

echo "==> Migrate job secrets (tangle-study-migrate)"
az containerapp job secret set \
  --name tangle-study-migrate \
  --resource-group "$RG" \
  --secrets "postgres-conn=${POSTGRES_CONNECTION_STRING}" \
  --output none

az containerapp job update \
  --name tangle-study-migrate \
  --resource-group "$RG" \
  --set-env-vars "ConnectionStrings__DefaultConnection=secretref:postgres-conn" \
  --output none

echo "==> Media worker secrets (tangle-study-worker-media)"
az containerapp secret set \
  --name tangle-study-worker-media \
  --resource-group "$RG" \
  --secrets \
    "blob-conn=${BLOB_CONNECTION_STRING}" \
    "worker-callback=${WORKER_CALLBACK_SECRET}" \
  --output none

az containerapp update \
  --name tangle-study-worker-media \
  --resource-group "$RG" \
  --set-env-vars \
    "AZURE_STORAGE_CONNECTION_STRING=secretref:blob-conn" \
    "WORKER_CALLBACK_SECRET=secretref:worker-callback" \
  --output none

if [[ -n "${GHCR_REGISTRY_USERNAME:-}" && -n "${GHCR_REGISTRY_PASSWORD:-}" ]]; then
  echo "==> GHCR registry credentials (private packages)"
  REGISTRY_APPS=(
    tangle-study-api
    tangle-study-web
    tangle-study-worker-chat
    tangle-study-worker-media
    tangle-study-worker-location
  )
  for app in "${REGISTRY_APPS[@]}"; do
    az containerapp registry set \
      --name "$app" \
      --resource-group "$RG" \
      --server ghcr.io \
      --username "$GHCR_REGISTRY_USERNAME" \
      --password "$GHCR_REGISTRY_PASSWORD" \
      --output none
  done
  az containerapp job registry set \
    --name tangle-study-migrate \
    --resource-group "$RG" \
    --server ghcr.io \
    --username "$GHCR_REGISTRY_USERNAME" \
    --password "$GHCR_REGISTRY_PASSWORD" \
    --output none
fi

echo "==> Secrets injected."
