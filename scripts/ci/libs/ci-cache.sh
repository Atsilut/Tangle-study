# shellcheck shell=bash
# Persistent CI caches under .ci-cache/ (gitignored). Source after setting ROOT.
#
# CLI: bash scripts/ci/libs/ci-cache.sh  — fix cache ownership after Docker writes.

ci_cache_root() {
  echo "${ROOT}/.ci-cache"
}

ensure_ci_cache_dirs() {
  mkdir -p \
    "$(ci_cache_root)/nuget" \
    "$(ci_cache_root)/npm" \
    "$(ci_cache_root)/cargo/registry" \
    "$(ci_cache_root)/cargo/git"
}

ci_nuget_mount() {
  ensure_ci_cache_dirs
  echo "$(ci_cache_root)/nuget:/tmp/nuget-packages"
}

# Docker runs as root by default; actions/cache tar fails on root-owned files.
ci_fix_cache_ownership() {
  [[ -n "${ROOT:-}" ]] || return 0

  local -a paths=()
  local p proj kind

  [[ -d "$(ci_cache_root)" ]] && paths+=("$(ci_cache_root)")

  p="${ROOT}/.ci-cache/publish"
  [[ -d "$p" ]] && paths+=("$p")

  p="${ROOT}/workers/target"
  [[ -d "$p" ]] && paths+=("$p")

  for proj in \
    Gateway Gateway.Tests \
    Tangle.AspNetCore Tangle.AspNetCore.Tests \
    TestSupport TestSupport.Scenarios \
    Users Users.Tests \
    Stack.Tests \
    Media Media.Tests \
    Chat Chat.Tests \
    Location Location.Tests \
    Community Community.Tests \
    Group Group.Tests \
    Social Social.Tests
  do
    for kind in bin obj; do
      p="${ROOT}/services/${proj}/${kind}"
      [[ -d "$p" ]] && paths+=("$p")
    done
  done

  p="${ROOT}/clients/web/node_modules"
  [[ -d "$p" ]] && paths+=("$p")

  if ((${#paths[@]} == 0)); then
    return 0
  fi

  if command -v sudo >/dev/null 2>&1; then
    sudo chown -R "$(id -u):$(id -g)" "${paths[@]}" 2>/dev/null || true
  fi
}

build_sdk_image() {
  local image_tag="${1:-tangle-study-sdk:local}"
  local dockerfile="${ROOT}/docker/Dockerfile.sdk"

  if [[ "${GITHUB_ACTIONS:-}" == "true" ]]; then
    docker buildx build \
      --cache-from "type=gha,scope=tangle-sdk" \
      --cache-to "type=gha,mode=max,scope=tangle-sdk" \
      -f "$dockerfile" \
      --build-arg "DOTNET_SDK_IMAGE=${DOTNET_SDK_IMAGE:?DOTNET_SDK_IMAGE is required}" \
      -t "$image_tag" \
      --load \
      "$ROOT"
  else
    docker build \
      -f "$dockerfile" \
      --build-arg "DOTNET_SDK_IMAGE=${DOTNET_SDK_IMAGE:?DOTNET_SDK_IMAGE is required}" \
      -t "$image_tag" \
      "$ROOT"
  fi
}

if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
  set -euo pipefail
  ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
  LOG_PREFIX="[CI][CACHE]"
  # shellcheck source=scripts/shared/common.sh
  source "$ROOT/scripts/shared/common.sh"
  log_step "FIX CI CACHE OWNERSHIP"
  ci_fix_cache_ownership
  log_info "cache ownership fixed"
fi
