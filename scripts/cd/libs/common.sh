#!/usr/bin/env bash
# shellcheck shell=bash
# Re-export shared logging for CD scripts (prefer scripts/shared/common.sh).

_libs_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/shared/common.sh
source "$_libs_dir/../../shared/common.sh"
