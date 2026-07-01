#!/usr/bin/env bash
# shellcheck shell=bash
# Shared CD logging. Set DEPLOY_LOG_PREFIX before sourcing (default: [DEPLOY]).

DEPLOY_LOG_PREFIX="${DEPLOY_LOG_PREFIX:-[DEPLOY]}"

log_info()  { echo "${DEPLOY_LOG_PREFIX}[INFO] $*"; }
log_step()  { echo ""; echo "========================================"; echo "${DEPLOY_LOG_PREFIX}[STEP] $*"; echo "========================================"; }
log_warn()  { echo "${DEPLOY_LOG_PREFIX}[WARN] $*"; }
log_error() { echo "${DEPLOY_LOG_PREFIX}[ERROR] $*" >&2; }

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
