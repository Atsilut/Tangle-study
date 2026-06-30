#!/usr/bin/env bash
set -euo pipefail

log_info()  { echo "[DEPLOY][INFO] $*"; }
log_step()  { echo ""; echo "========================================"; echo "[DEPLOY][STEP] $*"; echo "========================================"; }
log_warn()  { echo "[DEPLOY][WARN] $*"; }
log_error() { echo "[DEPLOY][ERROR] $*" >&2; }

fail() {
  log_error "$1"
  exit 1
}

############################################
log_step "SECRET & CONFIG DEPLOYMENT START"

log_info "resource-group=$AZURE_RESOURCE_GROUP"
log_info "target=azure-container-apps"
log_info "mode=rolling-revision-update"

############################################
log_step "VALIDATING REQUIRED ENVIRONMENT VARIABLES"

[[ -n "${POSTGRES_CONNECTION_STRING:-}" ]] || fail "missing POSTGRES_CONNECTION_STRING"
[[ -n "${BLOB_CONNECTION_STRING:-}" ]] || fail "missing BLOB_CONNECTION_STRING"
[[ -n "${JWT_SECRET:-}" ]] || fail "missing JWT_SECRET"
[[ -n "${WORKER_CALLBACK_SECRET:-}" ]] || fail "missing WORKER_CALLBACK_SECRET"
[[ -n "${METRICS_SCRAPE_SECRET:-}" ]] || fail "missing METRICS_SCRAPE_SECRET"

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

POSTGRES_EXPORTER_DSN="$(python3 scripts/lib/build_postgres_dsn.py <<< "$POSTGRES_CONNECTION_STRING")"

log_info "postgres-dsn: GENERATED"
log_info "secret-count: api=$(echo 6), exporter=$(echo 1)"

############################################
log_step "APPLYING SECRETS (tangle-study-api)"

az containerapp secret set \
  --name tangle-study-api \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --secrets \
    "postgres-conn=$POSTGRES_CONNECTION_STRING" \
    "blob-conn=$BLOB_CONNECTION_STRING" \
    "jwt-secret=$JWT_SECRET" \
    "worker-callback=$WORKER_CALLBACK_SECRET" \
    "metrics-secret=$METRICS_SCRAPE_SECRET" \
    "appinsights-conn=$APPLICATIONINSIGHTS_CONNECTION_STRING" \
  --output none

log_info "secrets applied: tangle-study-api"

############################################
log_step "DEPLOYING APPLICATION REVISION (tangle-study-api)"

az containerapp update \
  --name tangle-study-api \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --set-env-vars \
    "ConnectionStrings__DefaultConnection=secretref:postgres-conn" \
    "Media__ConnectionString=secretref:blob-conn" \
    "Jwt__Secret=secretref:jwt-secret" \
    "Media__WorkerCallbackSecret=secretref:worker-callback" \
    "Metrics__ScrapeSecret=secretref:metrics-secret" \
    "APPLICATIONINSIGHTS_CONNECTION_STRING=secretref:appinsights-conn" \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "ASPNETCORE_URLS=http://+:8080" \
  --output none

log_info "revision created: tangle-study-api (pending health validation)"

############################################
log_step "DEPLOYING POSTGRES EXPORTER"

az containerapp secret set \
  --name tangle-study-postgres-exporter \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --secrets "postgres-dsn=$POSTGRES_EXPORTER_DSN" \
  --output none

az containerapp update \
  --name tangle-study-postgres-exporter \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --set-env-vars "DATA_SOURCE_NAME=secretref:postgres-dsn" \
  --output none

log_info "revision created: postgres-exporter"

############################################
log_step "DEPLOYING PROMETHEUS"

az containerapp secret set \
  --name tangle-study-prometheus \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --secrets "metrics-secret=$METRICS_SCRAPE_SECRET" \
  --output none

az containerapp update \
  --name tangle-study-prometheus \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --set-env-vars "METRICS_SCRAPE_SECRET=secretref:metrics-secret" \
  --output none

log_info "revision created: prometheus"

############################################
log_step "DEPLOYING GRAFANA"

az containerapp secret set \
  --name tangle-study-grafana \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --secrets "grafana-admin-password=$GRAFANA_ADMIN_PASSWORD" \
  --output none

az containerapp update \
  --name tangle-study-grafana \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --set-env-vars "GF_SECURITY_ADMIN_PASSWORD=secretref:grafana-admin-password" \
  --output none

log_info "revision created: grafana"

############################################
log_step "DEPLOYING WORKERS"

deploy_worker () {
  local app="$1"

  log_info "worker-start: $app"

  az containerapp secret set \
    --name "$app" \
    --resource-group "$AZURE_RESOURCE_GROUP" \
    --secrets "metrics-secret=$METRICS_SCRAPE_SECRET" \
    --output none

  az containerapp update \
    --name "$app" \
    --resource-group "$AZURE_RESOURCE_GROUP" \
    --set-env-vars "METRICS_SCRAPE_SECRET=secretref:metrics-secret" \
    --output none

  log_info "worker-revision-created: $app"
}

deploy_worker "tangle-study-worker-chat"
deploy_worker "tangle-study-worker-location"
deploy_worker "tangle-study-worker-media"

############################################
log_step "DEPLOYMENT SUMMARY"

log_info "all-revisions-created=true"
log_info "traffic-status=unchanged (old revisions active)"
log_info "next-step=recommend health gate validation"
log_info "deployment-status=COMPLETED (pending verification)"