#!/usr/bin/env bash
# Reconcile API/web/worker runtime using HEALTH-BASED GATING ONLY.
#
# This script removes all cross-app probing and DNS inference.
# It relies on:
#   1. Container App Revision READY state (control plane)
#   2. API ingress /health endpoint (data plane)
#
# Required env:
#   AZURE_RESOURCE_GROUP
#
# Optional env:
#   API_APP_NAME (default: tangle-study-api)
#   WEB_APP_NAME (default: tangle-study-web)
#   WORKER_MEDIA_APP_NAME (default: tangle-study-worker-media)
#   WORKER_LOCATION_APP_NAME (default: tangle-study-worker-location)
#   API_TARGET_PORT (default: 8080)
#   DOMAIN (internal or external fqdn suffix if needed externally)
#   RUNTIME_WAIT_TIMEOUT_SEC (default: 300)

set -euo pipefail

: "${AZURE_RESOURCE_GROUP:?AZURE_RESOURCE_GROUP is required}"

RG="$AZURE_RESOURCE_GROUP"

API_APP="${API_APP_NAME:-tangle-study-api}"
WEB_APP="${WEB_APP_NAME:-tangle-study-web}"
WORKER_MEDIA_APP="${WORKER_MEDIA_APP_NAME:-tangle-study-worker-media}"
WORKER_LOCATION_APP="${WORKER_LOCATION_APP_NAME:-tangle-study-worker-location}"

API_TARGET_PORT="${API_TARGET_PORT:-8080}"
RUNTIME_WAIT_TIMEOUT_SEC="${RUNTIME_WAIT_TIMEOUT_SEC:-300}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=scripts/lib/azure-container-apps-readiness.sh
source "$ROOT/scripts/lib/azure-container-apps-readiness.sh"


########################################
# LOGGING
########################################
log_info()  { echo "[RECONCILE][INFO] $*"; }
log_step()  { echo ""; echo "========================================"; echo "[RECONCILE][STEP] $*"; echo "========================================"; }
log_error() { echo "[RECONCILE][ERROR] $*" >&2; }


########################################
# GET IMAGE
########################################
container_app_image() {
  az containerapp show \
    --name "$1" \
    --resource-group "$RG" \
    --query "properties.template.containers[0].image" \
    --output tsv 2>/dev/null || true
}

is_placeholder_image() {
  [[ "$1" == *"k8se/quickstart"* ]]
}


########################################
# WAIT FOR REVISION READY (SOURCE OF TRUTH)
########################################
wait_for_ready() {
  local app="$1"

  log_info "waiting-revision-ready app=$app timeout=${RUNTIME_WAIT_TIMEOUT_SEC}s"

  wait_for_container_app_revision_healthy "$app" "$RG" "$RUNTIME_WAIT_TIMEOUT_SEC"
}


########################################
# HEALTH CHECK (ONLY VALID RUNTIME CHECK)
########################################
check_api_health() {
  local url="https://${API_APP}.${DOMAIN}/health"

  log_info "checking-api-health url=$url"

  for i in {1..20}; do
    if curl -sf "$url" >/dev/null; then
      log_info "api-health=ok"
      return 0
    fi
    sleep 5
  done

  log_error "api-health=failed url=$url"
  return 1
}


########################################
# INGRESs FIX
########################################
ensure_api_target_port() {
  local current

  current="$(az containerapp show \
    --name "$API_APP" \
    --resource-group "$RG" \
    --query "properties.configuration.ingress.targetPort" \
    --output tsv 2>/dev/null || true)"

  if [[ "$current" == "$API_TARGET_PORT" ]]; then
    log_info "api-ingress-port already correct port=$API_TARGET_PORT"
    return 0
  fi

  log_info "updating-api-ingress-port from=${current:-unset} to=$API_TARGET_PORT"

  az containerapp ingress update \
    --name "$API_APP" \
    --resource-group "$RG" \
    --target-port "$API_TARGET_PORT" \
    --output none
}


########################################
# MAIN FLOW
########################################
log_step "START RECONCILE"

API_IMAGE="$(container_app_image "$API_APP")"

if [[ -z "$API_IMAGE" ]]; then
  log_error "api-not-found app=$API_APP"
  exit 0
fi

if is_placeholder_image "$API_IMAGE"; then
  log_info "placeholder-image detected skip-reconcile image=$API_IMAGE"
  exit 0
fi


########################################
log_step "ENSURE API INGRESS"
ensure_api_target_port


########################################
log_step "WAIT REVISION READY (API)"
wait_for_ready "$API_APP"


########################################
log_step "WAIT REVISION READY (WEB)"
wait_for_ready "$WEB_APP"


########################################
log_step "HEALTH GATE (API ONLY)"
check_api_health


########################################
log_step "UPDATE WEB UPSTREAM (NO PROBING)"
log_info "setting-web-upstream TANGLE_API_UPSTREAM=https://${API_APP}.${DOMAIN}"

az containerapp update \
  --name "$WEB_APP" \
  --resource-group "$RG" \
  --set-env-vars "TANGLE_API_UPSTREAM=https://${API_APP}.${DOMAIN}" \
  --output none


########################################
log_step "WAIT WEB RECONCILE"
wait_for_ready "$WEB_APP"


########################################
log_step "UPDATE WORKERS (STATIC BASE URL)"
for app in "$WORKER_MEDIA_APP" "$WORKER_LOCATION_APP"; do
  log_info "setting-worker-api-base-url app=$app"

  az containerapp update \
    --name "$app" \
    --resource-group "$RG" \
    --set-env-vars "API_BASE_URL=https://${API_APP}.${DOMAIN}" \
    --output none
done


log_step "RECONCILE COMPLETE"
log_info "status=success api=$API_APP web=$WEB_APP"