#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
LOG_PREFIX="[DEPLOY]"
# shellcheck source=scripts/shared/common.sh
source "$ROOT/scripts/shared/common.sh"

############################################
log_step "SECRET INJECTION START"

log_info "resource-group=$AZURE_RESOURCE_GROUP"
log_info "target=azure-container-apps"

############################################
log_step "VALIDATING REQUIRED ENVIRONMENT VARIABLES"

require_env AZURE_RESOURCE_GROUP
require_env POSTGRES_CONNECTION_STRING
require_env BLOB_CONNECTION_STRING
require_env JWT_SECRET
require_env WORKER_CALLBACK_SECRET
require_env METRICS_SCRAPE_SECRET
require_env GRAFANA_ADMIN_PASSWORD

log_info "environment validation: OK"

############################################
log_step "RESOLVING APPLICATION INSIGHTS"

if [[ -z "${APPLICATIONINSIGHTS_CONNECTION_STRING:-}" ]]; then
  log_info "fetching application insights connection string"
  APPLICATIONINSIGHTS_CONNECTION_STRING="$(az monitor app-insights component show \
    --app "${APP_INSIGHTS_NAME:-tanglestudyprod-appi}" \
    --resource-group "$AZURE_RESOURCE_GROUP" \
    --query connectionString -o tsv)"
fi

[[ -n "$APPLICATIONINSIGHTS_CONNECTION_STRING" ]] || fail "app insights resolution failed"
log_info "application insights: RESOLVED"

############################################
log_step "BUILDING SECRET PAYLOAD"

POSTGRES_EXPORTER_DSN="$(python3 "$ROOT/scripts/cd/libs/parse_postgres_conn.py" <<< "$POSTGRES_CONNECTION_STRING")"

log_info "postgres-dsn: GENERATED"

############################################
log_step "APPLYING SECRETS (tangle-study-api)"

api_secrets=(
  "postgres-conn=$POSTGRES_CONNECTION_STRING"
  "blob-conn=$BLOB_CONNECTION_STRING"
  "jwt-secret=$JWT_SECRET"
  "worker-callback=$WORKER_CALLBACK_SECRET"
  "metrics-secret=$METRICS_SCRAPE_SECRET"
  "appinsights-conn=$APPLICATIONINSIGHTS_CONNECTION_STRING"
  "places-api-key=${PLACES_API_KEY:-}"
)

az containerapp secret set \
  --name tangle-study-api \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --secrets "${api_secrets[@]}" \
  --output none

log_info "secrets applied: tangle-study-api"

############################################
log_step "APPLYING SECRETS (postgres-exporter)"

az containerapp secret set \
  --name tangle-study-postgres-exporter \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --secrets "postgres-dsn=$POSTGRES_EXPORTER_DSN" \
  --output none

log_info "secrets applied: tangle-study-postgres-exporter"

############################################
log_step "APPLYING SECRETS (prometheus)"

az containerapp secret set \
  --name tangle-study-prometheus \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --secrets "metrics-secret=$METRICS_SCRAPE_SECRET" \
  --output none

log_info "secrets applied: tangle-study-prometheus"

############################################
log_step "APPLYING SECRETS (grafana)"

az containerapp secret set \
  --name tangle-study-grafana \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --secrets "grafana-admin-password=$GRAFANA_ADMIN_PASSWORD" \
  --output none

log_info "secrets applied: tangle-study-grafana"

############################################
log_step "APPLYING SECRETS (workers)"

apply_worker_secrets() {
  local app="$1"; shift
  az containerapp secret set \
    --name "$app" \
    --resource-group "$AZURE_RESOURCE_GROUP" \
    --secrets "$@" \
    --output none
  log_info "secrets applied: $app"
}

apply_worker_secrets tangle-study-worker-chat \
  "metrics-secret=$METRICS_SCRAPE_SECRET"

apply_worker_secrets tangle-study-worker-location \
  "metrics-secret=$METRICS_SCRAPE_SECRET" \
  "worker-callback=$WORKER_CALLBACK_SECRET"

apply_worker_secrets tangle-study-worker-media \
  "metrics-secret=$METRICS_SCRAPE_SECRET" \
  "blob-conn=$BLOB_CONNECTION_STRING" \
  "worker-callback=$WORKER_CALLBACK_SECRET"

############################################
log_step "APPLYING SECRETS (tangle-study-migrate JOB)"

az containerapp job secret set \
  --name tangle-study-migrate \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --secrets "postgres-conn=$POSTGRES_CONNECTION_STRING" \
  --output none

log_info "secrets applied: tangle-study-migrate (job)"

############################################
log_step "SECRET INJECTION SUMMARY"

log_info "all-secrets-applied=true"
log_info "next-step=run update-image.sh / deploy.sh to roll out a new revision referencing these secrets"
log_info "injection-status=COMPLETED"