#!/usr/bin/env bash
# Idempotent Azure Container Apps deploy via Bicep.
# parameters.prod.json is the single source of truth for apps, env, and migrate jobs.
# Secure values are passed as Bicep @secure() parameters (never written into the JSON file).
#
# Usage (CD):
#   AZURE_RESOURCE_GROUP=tangle-study-prod IMAGE_TAG=<sha> \
#   POSTGRES_CONNECTION_STRING=... BLOB_CONNECTION_STRING=... ... \
#   bash scripts/cd/azure-cd-deploy-bicep.sh
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
LOG_PREFIX="[DEPLOY][INFRA]"
# shellcheck source=scripts/shared/common.sh
source "$ROOT/scripts/shared/common.sh"

require_env AZURE_RESOURCE_GROUP
require_env IMAGE_TAG
require_env POSTGRES_CONNECTION_STRING
require_env BLOB_CONNECTION_STRING
require_env JWT_SECRET
require_env WORKER_CALLBACK_SECRET
require_env METRICS_SCRAPE_SECRET
require_env GRAFANA_ADMIN_PASSWORD
require_env GATEWAY_SECRET
require_env INTERNAL_SERVICE_SECRET

PARAM_FILE="${PARAM_FILE:-infra/azure/parameters.prod.json}"
TEMPLATE_FILE="${TEMPLATE_FILE:-infra/azure/main.bicep}"
USE_PLACEHOLDER_IMAGES="${USE_PLACEHOLDER_IMAGES:-false}"

[[ -f "$PARAM_FILE" ]] || fail "missing parameter file: $PARAM_FILE"
[[ -f "$TEMPLATE_FILE" ]] || fail "missing template file: $TEMPLATE_FILE"

############################################
log_step "RESOLVE DERIVED SECRETS"

POSTGRES_EXPORTER_DSN="$(python3 "$ROOT/scripts/cd/libs/parse_postgres_conn.py" <<< "$POSTGRES_CONNECTION_STRING")"
[[ -n "$POSTGRES_EXPORTER_DSN" ]] || fail "failed to derive postgres exporter DSN"

if [[ -z "${APPLICATIONINSIGHTS_CONNECTION_STRING:-}" ]]; then
  log_info "fetching application insights connection string"
  APPLICATIONINSIGHTS_CONNECTION_STRING="$(az monitor app-insights component show \
    --app "${APP_INSIGHTS_NAME:-tanglestudyprod-appi}" \
    --resource-group "$AZURE_RESOURCE_GROUP" \
    --query connectionString -o tsv 2>/dev/null || true)"
fi

APPLICATIONINSIGHTS_CONNECTION_STRING="${APPLICATIONINSIGHTS_CONNECTION_STRING:-}"

############################################
log_step "DEPLOY BICEP"

log_info "rg=$AZURE_RESOURCE_GROUP template=$TEMPLATE_FILE param-file=$PARAM_FILE imageTag=$IMAGE_TAG"

extra_params=(
  --parameters "imageTag=${IMAGE_TAG}"
  --parameters "usePlaceholderImages=${USE_PLACEHOLDER_IMAGES}"
  --parameters "postgresConnectionString=${POSTGRES_CONNECTION_STRING}"
  --parameters "postgresExporterDsn=${POSTGRES_EXPORTER_DSN}"
  --parameters "blobConnectionString=${BLOB_CONNECTION_STRING}"
  --parameters "jwtSecret=${JWT_SECRET}"
  --parameters "workerCallbackSecret=${WORKER_CALLBACK_SECRET}"
  --parameters "metricsScrapeSecret=${METRICS_SCRAPE_SECRET}"
  --parameters "placesApiKey=${PLACES_API_KEY:-}"
  --parameters "applicationInsightsConnectionString=${APPLICATIONINSIGHTS_CONNECTION_STRING}"
  --parameters "grafanaAdminPassword=${GRAFANA_ADMIN_PASSWORD}"
  --parameters "gatewaySecret=${GATEWAY_SECRET}"
  --parameters "internalServiceSecret=${INTERNAL_SERVICE_SECRET}"
)

if [[ -n "${GHCR_REGISTRY_USERNAME:-}" && -n "${GHCR_REGISTRY_PASSWORD:-}" ]]; then
  extra_params+=(
    --parameters "registryUsername=${GHCR_REGISTRY_USERNAME}"
    --parameters "registryPassword=${GHCR_REGISTRY_PASSWORD}"
  )
fi

if [[ -n "${JWT_EXPIRY_MINUTES:-}" ]]; then
  log_info "JWT_EXPIRY_MINUTES is set (${JWT_EXPIRY_MINUTES}); ensure parameters.prod.json Jwt__ExpiryMinutes matches or update users env after deploy"
fi

az deployment group create \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --template-file "$TEMPLATE_FILE" \
  --parameters "@${PARAM_FILE}" \
  "${extra_params[@]}" \
  --output table

############################################
log_step "BICEP DEPLOY COMPLETE"
log_info "status=completed imageTag=$IMAGE_TAG"
