#!/usr/bin/env bash
set -euo pipefail

: "${AZURE_RESOURCE_GROUP:?}"

log_step() { echo "========================================"; echo "$1"; echo "========================================"; }
log_info() { echo "[INFO] $1"; }
log_error() { echo "[ERROR] $1" >&2; }

log_step "SMOKE TESTS"

# 1. Resolve web FQDN
WEB_FQDN=$(az containerapp show \
  --name tangle-study-web \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --query "properties.configuration.ingress.fqdn" -o tsv)

log_info "web_fqdn=$WEB_FQDN"

# 2. Wait for readiness
log_info "Waiting for Container Apps to be ready..."
for app in tangle-study-api tangle-study-web; do
  az containerapp revision list --name "$app" --resource-group "$AZURE_RESOURCE_GROUP" \
    --query "[?properties.provisioningState=='Provisioned' && properties.runningState=='Running']" \
    --output none || { log_error "$app not ready"; exit 1; }
done

# 3. Check web root (SPA)
log_info "Checking web root..."
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "https://${WEB_FQDN}/")
if [[ "$HTTP_CODE" != "200" ]]; then
  log_error "web root returned $HTTP_CODE"
  exit 1
fi
log_info "web root: $HTTP_CODE ✓"

# 4. Check API /health
log_info "Checking API /health (via web ingress)..."
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "https://${WEB_FQDN}/health")
if [[ "$HTTP_CODE" != "200" ]]; then
  log_error "api /health returned $HTTP_CODE"
  exit 1
fi
log_info "api /health: $HTTP_CODE ✓"

log_step "SMOKE TESTS PASSED"