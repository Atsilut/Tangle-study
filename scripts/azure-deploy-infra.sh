#!/usr/bin/env bash
# Deploy Tangle Azure infrastructure with Bicep (study / free-tier friendly).
#
# Postgres: Neon (external) — connection string injected by CD, not Bicep.
# Redis + monitoring exporters run as Container Apps; app images from GHCR.
#
# Prerequisites: Azure CLI (`az`), logged in (`az login`).
#
# Production (matches GitHub Environment `prod` / CD on main):
#   ./scripts/azure-deploy-infra.sh prod
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
# shellcheck source=scripts/ci/libs/versions-prod-env.sh
source "$ROOT/scripts/ci/libs/versions-prod-env.sh"
load_versions_prod_env "$ROOT"

TARGET="${1:-}"
LOCATION="${AZURE_LOCATION:-eastus}"
SUBSCRIPTION="${AZURE_SUBSCRIPTION_ID:-}"

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
  COMPOSE_ENV_FILE          Optional — pinned infra tags (default: docker/versions.prod.env)
  GHCR_REGISTRY_USERNAME    Optional — prefer GitHub secret + CD inject for private GHCR
  GHCR_REGISTRY_PASSWORD    Optional — GitHub PAT with read:packages
EOF
}

deploy_env() {
  local rg="$1"
  local parameters_file="$2"

  local extra_params=(
    --parameters "redisImage=${REDIS_IMAGE}"
    --parameters "prometheusImage=${PROMETHEUS_IMAGE}"
    --parameters "grafanaImage=${GRAFANA_IMAGE}"
    --parameters "postgresExporterImage=${POSTGRES_EXPORTER_IMAGE}"
    --parameters "redisExporterImage=${REDIS_EXPORTER_IMAGE}"
  )

  if [[ -n "${GHCR_REGISTRY_USERNAME:-}" && -n "${GHCR_REGISTRY_PASSWORD:-}" ]]; then
    extra_params+=(
      --parameters "registryUsername=${GHCR_REGISTRY_USERNAME}"
      --parameters "registryPassword=${GHCR_REGISTRY_PASSWORD}"
    )
  fi

  az group create --name "$rg" --location "$LOCATION" --output none
  az deployment group create \
    --resource-group "$rg" \
    --template-file infra/azure/main.bicep \
    --parameters "@${parameters_file}" \
    "${extra_params[@]}" \
    --output table
}

case "$TARGET" in
  dev) deploy_env "tangle-study-dev" "infra/azure/parameters.dev.json" ;;
  prod) deploy_env "tangle-study-prod" "infra/azure/parameters.prod.json" ;;
  -h|--help|"") usage; exit "${TARGET:+0}" 1 ;;
  *) echo "Unknown target: $TARGET" >&2; usage >&2; exit 1 ;;
esac
