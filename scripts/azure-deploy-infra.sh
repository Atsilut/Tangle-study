#!/usr/bin/env bash
# Deploy Tangle Azure infrastructure with Bicep (study / free-tier friendly).
#
# Postgres: Neon (external) — connection string injected as a secure Bicep param.
# Redis + monitoring exporters run as Container Apps; app images from GHCR.
# parameters.prod.json is the single source of truth for containerApps + migrateJobs.
#
# Prerequisites: Azure CLI (`az`), logged in (`az login`).
#
# Production (matches GitHub Environment `prod` / CD on main):
#   POSTGRES_CONNECTION_STRING=... BLOB_CONNECTION_STRING=... JWT_SECRET=... \
#   WORKER_CALLBACK_SECRET=... METRICS_SCRAPE_SECRET=... GRAFANA_ADMIN_PASSWORD=... \
#   GATEWAY_SECRET=... INTERNAL_SERVICE_SECRET=... \
#   ./scripts/azure-deploy-infra.sh prod
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
LOG_PREFIX="[DEPLOY][INFRA]"
# shellcheck source=scripts/shared/common.sh
source "$ROOT/scripts/shared/common.sh"

TARGET="${1:-}"
LOCATION="${AZURE_LOCATION:-eastus}"
SUBSCRIPTION="${AZURE_SUBSCRIPTION_ID:-}"
IMAGE_TAG="${IMAGE_TAG:-latest}"
USE_PLACEHOLDER_IMAGES="${USE_PLACEHOLDER_IMAGES:-true}"

if [[ -n "$SUBSCRIPTION" ]]; then
  az account set --subscription "$SUBSCRIPTION"
fi

usage() {
  cat <<'EOF'
Usage: ./scripts/azure-deploy-infra.sh <dev|prod>

  dev   Deploy dev stack into resource group tangle-study-dev (local experiments)
  prod  Deploy production stack into resource group tangle-study-prod (CD target)

Environment variables:
  AZURE_SUBSCRIPTION_ID     Optional subscription override
  AZURE_LOCATION            Azure region (default: eastus)
  IMAGE_TAG                 Container image tag (default: latest)
  USE_PLACEHOLDER_IMAGES    true|false (default: true for manual bootstrap)
  GHCR_REGISTRY_USERNAME    Optional — prefer GitHub secret + CD inject for private GHCR
  GHCR_REGISTRY_PASSWORD    Optional — GitHub PAT with read:packages

Secure params (required for a usable stack; optional for placeholder-only bootstrap):
  POSTGRES_CONNECTION_STRING, BLOB_CONNECTION_STRING, JWT_SECRET,
  WORKER_CALLBACK_SECRET, METRICS_SCRAPE_SECRET, GRAFANA_ADMIN_PASSWORD,
  GATEWAY_SECRET, INTERNAL_SERVICE_SECRET
  PLACES_API_KEY, APPLICATIONINSIGHTS_CONNECTION_STRING (optional)
EOF
}

deploy_env() {
  local rg="$1"
  local parameters_file="$2"

  [[ -f "$parameters_file" ]] || fail "missing parameter file: $parameters_file"

  local postgres_dsn=""
  if [[ -n "${POSTGRES_CONNECTION_STRING:-}" ]]; then
    postgres_dsn="$(python3 "$ROOT/scripts/cd/libs/parse_postgres_conn.py" <<< "$POSTGRES_CONNECTION_STRING")"
  fi

  local extra_params=(
    --parameters "imageTag=${IMAGE_TAG}"
    --parameters "usePlaceholderImages=${USE_PLACEHOLDER_IMAGES}"
    --parameters "postgresConnectionString=${POSTGRES_CONNECTION_STRING:-}"
    --parameters "postgresExporterDsn=${postgres_dsn}"
    --parameters "blobConnectionString=${BLOB_CONNECTION_STRING:-}"
    --parameters "jwtSecret=${JWT_SECRET:-}"
    --parameters "workerCallbackSecret=${WORKER_CALLBACK_SECRET:-}"
    --parameters "metricsScrapeSecret=${METRICS_SCRAPE_SECRET:-}"
    --parameters "placesApiKey=${PLACES_API_KEY:-}"
    --parameters "applicationInsightsConnectionString=${APPLICATIONINSIGHTS_CONNECTION_STRING:-}"
    --parameters "grafanaAdminPassword=${GRAFANA_ADMIN_PASSWORD:-}"
    --parameters "gatewaySecret=${GATEWAY_SECRET:-}"
    --parameters "internalServiceSecret=${INTERNAL_SERVICE_SECRET:-}"
  )

  if [[ -n "${GHCR_REGISTRY_USERNAME:-}" && -n "${GHCR_REGISTRY_PASSWORD:-}" ]]; then
    extra_params+=(
      --parameters "registryUsername=${GHCR_REGISTRY_USERNAME}"
      --parameters "registryPassword=${GHCR_REGISTRY_PASSWORD}"
    )
  fi

  log_step "DEPLOY $rg"

  az group create --name "$rg" --location "$LOCATION" --output none
  az deployment group create \
    --resource-group "$rg" \
    --template-file infra/azure/main.bicep \
    --parameters "@${parameters_file}" \
    "${extra_params[@]}" \
    --output table

  log_info "deployment completed rg=$rg"
}

case "$TARGET" in
  dev) deploy_env "tangle-study-dev" "infra/azure/parameters.dev.json" ;;
  prod) deploy_env "tangle-study-prod" "infra/azure/parameters.prod.json" ;;
  -h|--help|"") usage; exit "${TARGET:+0}" 1 ;;
  *) usage >&2; fail "unknown target: $TARGET" ;;
esac
