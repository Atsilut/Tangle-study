#!/usr/bin/env bash
# Deploy Tangle Azure infrastructure with Bicep (study / free-tier friendly).
#
# No managed PostgreSQL, Redis, or ACR — Postgres and Redis run as Container Apps;
# app images are pulled from GHCR (public packages need no registry password).
#
# Prerequisites: Azure CLI (`az`), logged in (`az login`).
#
# Production (matches GitHub Environment `production` / CD on main):
#   POSTGRES_ADMIN_PASSWORD='...' ./scripts/azure-deploy-infra.sh prod
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

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
  POSTGRES_ADMIN_PASSWORD   Required (min 8 chars) — must match GitHub secret POSTGRES_ADMIN_PASSWORD
  GHCR_REGISTRY_USERNAME    Optional — prefer GitHub secret + CD inject for private GHCR
  GHCR_REGISTRY_PASSWORD    Optional — GitHub PAT with read:packages
EOF
}

deploy_env() {
  local rg="$1"
  local parameters_file="$2"

  if [[ -z "${POSTGRES_ADMIN_PASSWORD:-}" ]]; then
    echo "POSTGRES_ADMIN_PASSWORD is required." >&2
    exit 1
  fi

  local extra_params=(
    --parameters "postgresAdminPassword=${POSTGRES_ADMIN_PASSWORD}"
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
