#!/usr/bin/env bash
# shellcheck shell=bash
# Shared script logging. Set LOG_PREFIX before sourcing (default: [DEPLOY]).
#
# Entrypoints use 44-char section dividers plus log_step:
#   ############################################
#   log_step "SECTION NAME IN ALL CAPS"

LOG_PREFIX="${LOG_PREFIX:-[DEPLOY]}"
# Backward-compatible alias for older scripts.
DEPLOY_LOG_PREFIX="${LOG_PREFIX}"

log_info()  { echo "${LOG_PREFIX}[INFO] $*"; }
log_step()  { echo ""; echo "========================================"; echo "${LOG_PREFIX}[STEP] $*"; echo "========================================"; }
log_warn()  { echo "${LOG_PREFIX}[WARN] $*"; }
log_error() { echo "${LOG_PREFIX}[ERROR] $*" >&2; }

# Collapsible subsection in GitHub Actions; plain divider locally.
# Use for work inside a log_step — do not call log_step for every sub-task.
log_group_start() {
  if [[ "${GITHUB_ACTIONS:-}" == "true" ]]; then
    echo "::group::$*"
  else
    echo ""
    echo "-------- $* --------"
  fi
}

log_group_end() {
  if [[ "${GITHUB_ACTIONS:-}" == "true" ]]; then
    echo "::endgroup::"
  fi
}

fail() {
  log_error "$1"
  exit 1
}

require_env() {
  local var_name="$1"
  if [[ -z "${!var_name:-}" ]]; then
    fail "missing $var_name"
  fi
}

# Fail if any named variable's value contains whitespace (space, tab, CR, LF).
# Secrets forwarded as HTTP headers (X-Gateway-Secret / X-Internal-Secret / X-Metrics-Secret
# / X-Worker-Callback-Secret) cannot carry whitespace — HTTP drops it and constant-time
# comparisons then fail. Reject rather than mutate. Do not use on connection strings
# (Npgsql "SSL Mode=Require" has a space).
require_no_whitespace() {
  local _name
  for _name in "$@"; do
    if [[ "${!_name-}" =~ [[:space:]] ]]; then
      fail "$_name must not contain whitespace (forwarded as an HTTP header)"
    fi
  done
}

# Fail if the named variable's value is shorter than the given minimum length.
require_min_length() {
  local var_name="$1" min="$2" val="${!1-}"
  if (( ${#val} < min )); then
    fail "$var_name must be at least $min characters (got ${#val})"
  fi
}

# Fail if any two named variables share the same non-empty value. Enforces unique
# per-service receive secrets: callers send the callee's secret, so reuse collapses
# service isolation. Empty values are ignored (placeholder bootstrap).
require_distinct() {
  local -a names=("$@")
  local i j
  for (( i = 0; i < ${#names[@]}; i++ )); do
    for (( j = i + 1; j < ${#names[@]}; j++ )); do
      if [[ -n "${!names[i]-}" && "${!names[i]-}" == "${!names[j]-}" ]]; then
        fail "${names[i]} and ${names[j]} must differ (unique secret per service)"
      fi
    done
  done
}
