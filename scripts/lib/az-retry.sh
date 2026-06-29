#!/usr/bin/env bash
# Retry az CLI invocations on transient Azure/network failures (GHA runners, ARM blips).

_lib_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/lib/log-redact.sh
source "$_lib_dir/log-redact.sh"

az_is_transient_failure() {
  local err_file="$1"
  grep -qiE \
    'Connection reset by peer|Connection aborted|ConnectionError|Connection broken|Read timed out|Temporary failure|temporarily unavailable|ProtocolError|RemoteDisconnected|Max requests|Too Many Requests|429|502|503|504|ServiceUnavailable|InternalServerError|Gateway Timeout|The operation was canceled|Operation timed out|EOF occurred|Resource temporarily unavailable' \
    "$err_file"
}

# Usage: az_retry az containerapp secret set --name ... (stdout captured on success)
az_retry() {
  local max_attempts="${AZ_RETRY_ATTEMPTS:-5}"
  local interval="${AZ_RETRY_INTERVAL_SEC:-15}"
  local attempt output_file rc result

  output_file="$(mktemp)"
  for (( attempt=1; attempt<=max_attempts; attempt++ )); do
    if result=$("$@" 2>"$output_file"); then
      rm -f "$output_file"
      printf '%s' "$result"
      return 0
    fi
    rc=$?
    if (( attempt < max_attempts )) && az_is_transient_failure "$output_file"; then
      echo "==> az CLI transient failure (attempt ${attempt}/${max_attempts}); retry in ${interval}s..." >&2
      tail -8 "$output_file" | redact_log_stream >&2 || true
      sleep "$interval"
      continue
    fi
    redact_log_stream <"$output_file" >&2
    rm -f "$output_file"
    return "$rc"
  done

  rm -f "$output_file"
  return 1
}
