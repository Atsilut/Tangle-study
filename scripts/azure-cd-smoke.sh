#!/usr/bin/env bash
# Post-deploy smoke checks against the public web Container App.
#
# Required env:
#   AZURE_RESOURCE_GROUP
#
# Optional env:
#   WEB_APP_NAME (default: tangle-web)
#   SMOKE_TIMEOUT_SEC (default: 300)
#
set -euo pipefail

: "${AZURE_RESOURCE_GROUP:?AZURE_RESOURCE_GROUP is required}"

WEB_APP="${WEB_APP_NAME:-tangle-web}"
TIMEOUT="${SMOKE_TIMEOUT_SEC:-300}"
RG="$AZURE_RESOURCE_GROUP"

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

check_url() {
  local path="$1"
  local expected="${2:-}"
  local url="${base_url}${path}"
  echo "    GET ${url}"
  local body
  body="$(curl -fsS --max-time 30 "$url")"
  if [[ -n "$expected" && "$body" != *"$expected"* ]]; then
    echo "Expected response to contain '${expected}', got: ${body}" >&2
    exit 1
  fi
}

# Container Apps may need a short warm-up after revision update.
for attempt in 1 2 3 4 5 6; do
  if curl -fsS --max-time 30 "${base_url}/health" >/dev/null 2>&1; then
    break
  fi
  if (( attempt == 6 )); then
    echo "Health check failed after retries" >&2
    exit 1
  fi
  echo "    waiting for /health (attempt ${attempt}/6)..."
  sleep 15
done

check_url "/health" "Healthy"
check_url "/" ""

echo "==> Smoke checks passed."
