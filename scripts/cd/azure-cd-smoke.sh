#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
LOG_PREFIX="[DEPLOY][SMOKE]"
# shellcheck source=scripts/shared/common.sh
source "$ROOT/scripts/shared/common.sh"

require_env AZURE_RESOURCE_GROUP

############################################
log_step "SMOKE TESTS"

WEB_FQDN=$(az containerapp show \
  --name tangle-study-web \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --query "properties.configuration.ingress.fqdn" -o tsv)

log_info "web_fqdn=$WEB_FQDN"

for app in tangle-study-web; do
  az containerapp revision list --name "$app" --resource-group "$AZURE_RESOURCE_GROUP" \
    --query "[?properties.provisioningState=='Provisioned' && properties.runningState=='Running']" \
    --output none || fail "$app not ready"
done

log_info "container apps ready"

HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "https://${WEB_FQDN}/")
[[ "$HTTP_CODE" == "200" ]] || fail "web root returned $HTTP_CODE"
log_info "web root: $HTTP_CODE OK"

HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "https://${WEB_FQDN}/health")
[[ "$HTTP_CODE" == "200" ]] || fail "api /health returned $HTTP_CODE"
log_info "api /health: $HTTP_CODE OK"

log_info "smoke tests passed"
