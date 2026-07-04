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

if [[ " $* " == *" --no-build "* ]]; then
  require_test_build_output() {
    local proj="$1"
    local dll="$2"
    [[ -f "${ROOT}/services/${proj}/bin/Release/net10.0/${dll}" ]] \
      || fail "missing services/${proj}/bin/Release/net10.0/${dll} — run ./scripts/ci/dotnet-publish.sh first"
  }

  case " $* " in
    *Api.Tests*)
      require_test_build_output "Api.Tests" "Api.Tests.dll"
      ;;
    *Media.Tests*)
      require_test_build_output "Media.Tests" "Media.Tests.dll"
      ;;
    *Chat.Tests*)
      require_test_build_output "Chat.Tests" "Chat.Tests.dll"
      ;;
    *Location.Tests*)
      require_test_build_output "Location.Tests" "Location.Tests.dll"
      ;;
    *)
      require_test_build_output "Api.Tests" "Api.Tests.dll"
      require_test_build_output "Media.Tests" "Media.Tests.dll"
      require_test_build_output "Chat.Tests" "Chat.Tests.dll"
      require_test_build_output "Location.Tests" "Location.Tests.dll"
      ;;
  esac
fi

log_step "BUILD SDK IMAGE"
build_sdk_image tangle-study-sdk:local

log_step "RUN INTEGRATION TESTS"
nuget_mount="$(ci_nuget_mount)"
tangle_compose --profile test run --rm \
  -v "$nuget_mount" \
  test "$@"

ci_fix_cache_ownership

log_info "integration tests completed"
