#!/usr/bin/env bash
# =========================================================
# [DEPLOY][REDIS] ACA Redis TCP Ingress & API Dependency Gate
# =========================================================
#
# PURPOSE:
#   Redis is a HARD dependency for API startup.
#   This script guarantees Redis readiness BEFORE API deploy.
#
# FLOW:
#   1. Ensure Redis ingress (control plane)
#   2. Validate Redis TCP connectivity (blocking gate)
#   3. Deploy API (only after Redis is healthy)
#   4. Wait API revision READY
#
# =========================================================

set -euo pipefail

: "${AZURE_RESOURCE_GROUP:?AZURE_RESOURCE_GROUP is required}"

RG="$AZURE_RESOURCE_GROUP"
REDIS_APP="${REDIS_APP_NAME:-tangle-study-redis}"
API_APP="${API_APP_NAME:-tangle-study-api}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT/scripts/lib/azure-container-apps-readiness.sh"
source "$ROOT/scripts/lib/azure-redis-readiness.sh"


# ---------------------------------------------------------
# LOGGING
# ---------------------------------------------------------
log_step()  { echo ""; echo "========================================"; echo "[DEPLOY][REDIS][STEP] $*"; echo "========================================"; }
log_info()  { echo "[DEPLOY][REDIS][INFO] $*"; }
log_warn()  { echo "[DEPLOY][REDIS][WARN] $*"; }
log_error() { echo "[DEPLOY][REDIS][ERROR] $*" >&2; }


# ---------------------------------------------------------
# PRECHECK
# ---------------------------------------------------------
log_step "PRECHECK - REDIS EXISTENCE"

if ! az containerapp show --name "$REDIS_APP" --resource-group "$RG" &>/dev/null; then
  log_error "Redis app not found: $REDIS_APP"
  exit 1
fi

log_info "Redis app found: $REDIS_APP"


# ---------------------------------------------------------
# STEP 1 - ENSURE INGRESs
# ---------------------------------------------------------
log_step "STEP 1 - ENSURE REDIS TCP INGRESS"

ensure_redis_tcp_ingress

log_info "Redis ingress configured"


# ---------------------------------------------------------
# STEP 2 - STRICT DEPENDENCY GATE
# ---------------------------------------------------------
log_step "STEP 2 - REDIS TCP READINESS GATE (BLOCKING)"

log_info "validating Redis TCP connectivity from API network..."

if ensure_redis_cross_app_tcp; then
  log_info "Redis TCP connectivity: OK"
else
  log_error "Redis TCP connectivity: FAILED"
  log_error "BLOCKING API deployment (dependency not satisfied)"
  exit 1
fi


# ---------------------------------------------------------
# STEP 3 - API DEPLOY
# ---------------------------------------------------------
log_step "STEP 3 - API DEPLOYMENT"

log_info "starting API update: $API_APP"

az containerapp update \
  --name "$API_APP" \
  --resource-group "$RG" \
  --output none

log_info "API deployment triggered"


# ---------------------------------------------------------
# STEP 4 - WAIT API READY
# ---------------------------------------------------------
log_step "STEP 4 - WAIT API REVISION READY"

wait_for_container_app_revision_healthy \
  "$API_APP" \
  "$RG" \
  "${API_READY_TIMEOUT_SEC:-300}"

log_info "API revision is READY"


# ---------------------------------------------------------
# FINAL
# ---------------------------------------------------------
log_step "DEPLOY COMPLETE"

log_info "status=SUCCESS api=$API_APP redis=$REDIS_APP"