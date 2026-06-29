#!/usr/bin/env bash
# Post-deploy smoke checks against the public web Container App.
#
# Required env:
#   AZURE_RESOURCE_GROUP
#
# Optional env:
#   WEB_APP_NAME (default: tangle-study-web)
#   API_APP_NAME (default: tangle-study-api)
#   SMOKE_TIMEOUT_SEC (default: 300)
#   SMOKE_SKIP_API_WAIT (set to 1 to skip ACA revision readiness wait)
#
set -euo pipefail

: "${AZURE_RESOURCE_GROUP:?AZURE_RESOURCE_GROUP is required}"

WEB_APP="${WEB_APP_NAME:-tangle-study-web}"
API_APP="${API_APP_NAME:-tangle-study-api}"
TIMEOUT="${SMOKE_TIMEOUT_SEC:-300}"
RG="$AZURE_RESOURCE_GROUP"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=scripts/lib/azure-container-apps-readiness.sh
source "$ROOT/scripts/lib/azure-container-apps-readiness.sh"
# shellcheck source=scripts/lib/log-redact.sh
source "$ROOT/scripts/lib/log-redact.sh"

LAST_HTTP_CODE=""

echo "==> Resolving FQDN for ${WEB_APP}"
deadline=$((SECONDS + TIMEOUT))
fqdn=""

while (( SECONDS < deadline )); do
  fqdn="$(az containerapp show \
    --name "$WEB_APP" \
    --resource-group "$RG" \
    --query properties.configuration.ingress.fqdn \
    --output tsv 2>/dev/null || true)"
  if [[ -n "$fqdn" ]]; then
    break
  fi
  sleep 10
done

if [[ -z "$fqdn" ]]; then
  echo "Could not resolve ingress FQDN for ${WEB_APP} within ${TIMEOUT}s" >&2
  exit 1
fi

base_url="https://${fqdn}"
echo "==> Smoke target: ${base_url}"

dump_smoke_failure_context() {
  dump_container_app_revision_status "$API_APP" "$RG"
  dump_container_app_revision_status "$WEB_APP" "$RG"

  local web_upstream
  web_upstream="$(az containerapp show \
    --name "$WEB_APP" \
    --resource-group "$RG" \
    --query "properties.template.containers[0].env[?name=='TANGLE_API_UPSTREAM'].value | [0]" \
    --output tsv 2>/dev/null || true)"
  echo "==> ${WEB_APP} TANGLE_API_UPSTREAM=${web_upstream:-unset}" >&2

  case "${LAST_HTTP_CODE}" in
    000)
      echo "Hint: HTTP 000 = curl timed out with no response." >&2
      echo "      API revision may still be activating, readiness may be failing," >&2
      echo "      or nginx is waiting on an unreachable ${API_APP} upstream." >&2
      ;;
    404)
      echo "Hint: HTTP 404 on /health often means API targetPort/upstream mismatch" >&2
      echo "      (placeholder 80 vs real API 8080). Run scripts/azure-cd-ensure-api-web-runtime.sh" >&2
      ;;
    502|503|504)
      echo "Hint: HTTP ${LAST_HTTP_CODE} = web reached ${API_APP} but /health failed or timed out." >&2
      echo "      Check ${API_APP} logs; Postgres or Redis dependency checks may be Unhealthy." >&2
      echo "      HTTP 504 often means nginx cannot reach the API upstream in time." >&2
      echo "      Verify TANGLE_API_UPSTREAM uses tangle-study-api.internal.<cae-domain>:8080" >&2
      echo "      (short name tangle-study-api:8080 can hang from web on ACA)." >&2
      ;;
    *)
      echo "Hint: /health is proxied to ${API_APP}. Check both app revision status above." >&2
      ;;
  esac
}

curl_check() {
  local path="$1"
  local expected="${2:-}"
  local url="${base_url}${path}"
  local body_file code body

  body_file="$(mktemp)"
  code="$(curl -sS --max-time 30 -o "$body_file" -w '%{http_code}' "$url" || echo "000")"
  body="$(cat "$body_file")"
  rm -f "$body_file"
  LAST_HTTP_CODE="$code"

  echo "    GET ${url} -> HTTP ${code}"
  if [[ "$code" == "000" ]]; then
    echo "    curl failed (no response)" >&2
    return 1
  fi

  if [[ "$code" != "200" ]]; then
    echo "    response body (first 400 chars): $(redact_log_text "${body:0:400}")" >&2
    return 1
  fi

  if [[ -n "$expected" && "$body" != *"$expected"* ]]; then
    echo "    expected body to contain '${expected}', got: $(redact_log_text "${body:0:400}")" >&2
    return 1
  fi

  return 0
}

if [[ "${SMOKE_SKIP_API_WAIT:-}" != "1" ]]; then
  echo "==> Waiting for ${API_APP} revision to become Healthy"
  wait_for_container_app_revision_healthy "$API_APP" "$RG" "$TIMEOUT"

  echo "==> Waiting for ${WEB_APP} revision to become Healthy"
  wait_for_container_app_revision_healthy "$WEB_APP" "$RG" "$TIMEOUT"
else
  echo "==> WARNING: SMOKE_SKIP_API_WAIT=1; skipping revision readiness wait" >&2
fi

# Static SPA shell first — confirms external web ingress without API.
for attempt in 1 2 3 4 5 6; do
  if curl_check "/" ""; then
    break
  fi
  if (( attempt == 6 )); then
    echo "SPA shell check failed after retries" >&2
    dump_smoke_failure_context
    exit 1
  fi
  echo "    waiting for / (attempt ${attempt}/6)..."
  sleep 15
done

echo "==> Verifying ${API_APP} /health inside the API pod"
if ! probe_api_health_via_exec "$API_APP" "$RG" 8080; then
  echo "API /health failed inside ${API_APP}; skipping public /health probe" >&2
  dump_smoke_failure_context
  exit 1
fi

# /health is proxied to the internal API through web nginx.
for attempt in 1 2 3 4 5 6; do
  if curl_check "/health" "Healthy"; then
    break
  fi
  if (( attempt == 6 )); then
    echo "Health check failed after retries" >&2
    dump_smoke_failure_context
    exit 1
  fi
  echo "    waiting for /health (attempt ${attempt}/6)..."
  sleep 15
done

echo "==> Smoke checks passed."
