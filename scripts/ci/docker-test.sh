#!/usr/bin/env bash
# Integration tests (Testcontainers). Mounts host Docker socket — not for build/EF (see docker-dotnet.sh).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
LOG_PREFIX="[CI][TEST]"
# shellcheck source=scripts/shared/common.sh
source "$ROOT/scripts/shared/common.sh"
# shellcheck source=scripts/shared/compose-env.sh
source "$ROOT/scripts/shared/compose-env.sh"
# shellcheck source=scripts/ci/libs/ci-cache.sh
source "$ROOT/scripts/ci/libs/ci-cache.sh"
# shellcheck source=scripts/ci/libs/versions-prod-env.sh
source "$ROOT/scripts/ci/libs/versions-prod-env.sh"
load_versions_prod_env "$ROOT"

log_step "BUILD SDK IMAGE"
build_sdk_image tangle-study-sdk:local

log_step "RUN INTEGRATION TESTS"
nuget_mount="$(ci_nuget_mount)"
tangle_compose --profile test run --rm \
  -v "$nuget_mount" \
  test "$@"

ci_fix_cache_ownership

log_info "integration tests completed"
