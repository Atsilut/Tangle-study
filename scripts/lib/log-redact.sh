#!/usr/bin/env bash
# Redact secrets and infrastructure endpoints from CD log output.

_log_redact_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
_log_redact_py="${_log_redact_dir}/log-redact.py"

redact_log_stream() {
  python3 "$_log_redact_py"
}

redact_log_text() {
  if (($# == 0)); then
    redact_log_stream
    return
  fi
  printf '%s' "$1" | redact_log_stream
}

# Log-friendly Redis endpoint label (no internal FQDN or option string).
redis_endpoint_log_label() {
  local host="$1"
  local port="${2:-6379}"
  local app="${REDIS_APP:-redis}"

  if [[ -z "$host" || "$host" == "$app" || "$host" == *".internal."* ]]; then
    echo "${app}:${port}"
    return 0
  fi

  echo "[redis-endpoint]:${port}"
}
