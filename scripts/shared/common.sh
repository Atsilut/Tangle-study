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
