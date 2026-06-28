# shellcheck shell=bash
# Persistent CI caches under .ci-cache/ (gitignored). Source after setting ROOT.

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

ci_cargo_registry_mount() {
  ensure_ci_cache_dirs
  echo "$(ci_cache_root)/cargo/registry:/usr/local/cargo/registry"
}

ci_cargo_git_mount() {
  ensure_ci_cache_dirs
  echo "$(ci_cache_root)/cargo/git:/usr/local/cargo/git"
}

ci_nuget_mount() {
  ensure_ci_cache_dirs
  echo "$(ci_cache_root)/nuget:/tmp/nuget-packages"
}

ci_npm_mount() {
  ensure_ci_cache_dirs
  echo "$(ci_cache_root)/npm:/npm-cache"
}

ci_runner_docker_user_args() {
  echo "--user $(id -u):$(id -g)"
}

# Docker runs as root by default; actions/cache tar fails on root-owned files.
ci_fix_cache_ownership() {
  if [[ ! -d "$(ci_cache_root)" ]]; then
    return 0
  fi
  if command -v sudo >/dev/null 2>&1; then
    sudo chown -R "$(id -u):$(id -g)" "$(ci_cache_root)" 2>/dev/null || true
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
