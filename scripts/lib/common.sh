#!/usr/bin/env bash
# shellcheck shell=bash

log_info()  { echo "[DEPLOY][INFO] $*"; }
log_step()  { echo ""; echo "========================================"; echo "[DEPLOY][STEP] $*"; echo "========================================"; }
log_warn()  { echo "[DEPLOY][WARN] $*"; }
log_error() { echo "[DEPLOY][ERROR] $*" >&2; }

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