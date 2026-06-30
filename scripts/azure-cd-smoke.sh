#!/usr/bin/env bash
# =========================================================
# [DEPLOY][SMOKE] Post-deploy validation gate
# =========================================================
#
# LAYERS:
#   1. ACA revision readiness
#   2. Internal API correctness (exec)
#   3. Cross-app networking correctness (exec)
#   4. External ingress correctness (curl)
#
# =========================================================

set -euo pipefail

: "${AZURE_RESOURCE_GROUP:?AZURE_RESOURCE_GROUP is required}"

WEB_APP="${WEB_APP_NAME:-tangle-study-web}"
API_APP="${API_APP_NAME:-tangle-study-api}"
TIMEOUT="${SMOKE_TIMEOUT_SEC:-300}"
RG="$AZURE_RESOURCE_GROUP"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT/scripts/lib/azure-container-apps-readiness.sh"
source "$ROOT/scripts/lib/log-redact.sh"


########################################
# LOGGING
########################################
log_step()  { echo ""; echo "========================================"; echo "[DEPLOY][SMOKE][STEP] $*"; echo "========================================"; }
log_info()  { echo "[DEPLOY][SMOKE][INFO] $*"; }
log_error() { echo "[DEPLOY][SMOKE][ERROR] $*" >&2; }


LAST_HTTP_CODE=""


########################################
# CONTEXT DUMP
########################################
dump_failure_context() {
  log_error "dumping-diagnostic-context"

  dump_container_app_revision_status "$API_APP" "$RG"
  dump_container_app_revision_status "$WEB_APP" "$RG"

  local upstream
  upstream="$(az containerapp show \
    --name "$WEB_APP" \
    --resource-group "$RG" \
    --query "properties.template.containers[0].env[?name=='TANGLE_API_UPSTREAM'].value | [0]" \
    --output tsv 2>/dev/null || true)"

  log_error "WEB_TANGLE_API_UPSTREAM=${upstream:-unset}"
  log_error "LAST_HTTP_CODE=${LAST_HTTP_CODE}"

  case "$LAST_HTTP_CODE" in
    000)
      log_error "CAUSE=NO_RESPONSE (network / ingress / timeout)"
      ;;
    404)
      log_error "CAUSE=NOT_FOUND (likely upstream port mismatch)"
      ;;
    502|503|504)
      log_error "CAUSE=UPSTREAM_FAILURE (API unreachable or unhealthy)"
      ;;
    *)
      log_error "CAUSE=UNKNOWN"
      ;;
  esac
}


########################################
# CURL PROBE
########################################
base_url=""

curl_check() {
  local path="$1"
  local expect="$2"
  local url="${base_url}${path}"

  local tmp body code
  tmp="$(mktemp)"

  code="$(curl -sS --max-time 30 -o "$tmp" -w '%{http_code}' "$url" || echo "000")"
  body="$(cat "$tmp")"
  rm -f "$tmp"

  LAST_HTTP_CODE="$code"

  log_info "HTTP_CHECK url=${url} code=${code}"

  if [[ "$code" != "200" ]]; then
    log_error "response=$(redact_log_text "${body:0:300}")"
    return 1
  fi

  if [[ -n "$expect" && "$body" != *"$expect"* ]]; then
    log_error "unexpected_body=$(redact_log_text "${body:0:300}")"
    return 1
  fi

  return 0
}


########################################
log_step "RESOLVE WEB FQDN"

fqdn=""
deadline=$((SECONDS + TIMEOUT))

while (( SECONDS < deadline )); do
  fqdn="$(az containerapp show \
    --name "$WEB_APP" \
    --resource-group "$RG" \
    --query properties.configuration.ingress.fqdn \
    --output tsv 2>/dev/null || true)"

  [[ -n "$fqdn" ]] && break
  sleep 5
done

if [[ -z "$fqdn" ]]; then
  log_error "failed_to_resolve_fqdn app=${WEB_APP}"
  exit 1
fi

base_url="https://${fqdn}"
log_info "base_url=${base_url}"


########################################
log_step "WAIT FOR ACA READINESS"

wait_for_container_app_revision_healthy "$API_APP" "$RG" "$TIMEOUT"
wait_for_container_app_revision_healthy "$WEB_APP" "$RG" "$TIMEOUT"


########################################
log_step "STATIC WEB INGRESS CHECK"

for i in {1..6}; do
  if curl_check "/" ""; then
    break
  fi

  [[ $i == 6 ]] && {
    log_error "stage=web_root_failed"
    dump_failure_context
    exit 1
  }

  sleep 10
done


########################################
log_step "INTERNAL API HEALTH (EXEC)"

if ! probe_api_health_via_exec "$API_APP" "$RG" 8080; then
  log_error "stage=api_internal_health_failed"
  dump_failure_context
  exit 1
fi


########################################
log_step "CROSS APP CONNECTIVITY (EXEC)"

if ! probe_web_api_health_via_exec "$WEB_APP" "$RG" "$API_APP"; then
  log_error "stage=web_to_api_failed"
  dump_failure_context
  exit 1
fi


########################################
log_step "PUBLIC HEALTH (INGRESS → WEB → API)"

for i in {1..6}; do
  if curl_check "/health" "Healthy"; then
    break
  fi

  [[ $i == 6 ]] && {
    log_error "stage=public_health_failed"
    dump_failure_context
    exit 1
  }

  sleep 10
done


########################################
log_step "SMOKE SUCCESS"

log_info "status=PASS app=${WEB_APP}"