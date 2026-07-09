#!/usr/bin/env bash
# Publish Gateway, Users, Media, Chat, Location, Community, Group, and Social for runtime Dockerfiles; build test projects for CI --no-build runs.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
LOG_PREFIX="[CI][DOTNET]"
# shellcheck source=scripts/shared/common.sh
source "$ROOT/scripts/shared/common.sh"
# shellcheck source=scripts/shared/compose-env.sh
source "$ROOT/scripts/shared/compose-env.sh"
# shellcheck source=scripts/ci/libs/ci-cache.sh
source "$ROOT/scripts/ci/libs/ci-cache.sh"
# shellcheck source=scripts/ci/libs/versions-prod-env.sh
source "$ROOT/scripts/ci/libs/versions-prod-env.sh"
load_versions_prod_env "$ROOT"

CONFIGURATION="${CONFIGURATION:-Release}"
GATEWAY_PUBLISH_DIR=".ci-cache/publish/gateway"
USERS_PUBLISH_DIR=".ci-cache/publish/users"
MEDIA_PUBLISH_DIR=".ci-cache/publish/media"
CHAT_PUBLISH_DIR=".ci-cache/publish/chat"
LOCATION_PUBLISH_DIR=".ci-cache/publish/location"
COMMUNITY_PUBLISH_DIR=".ci-cache/publish/community"
GROUP_PUBLISH_DIR=".ci-cache/publish/group"
SOCIAL_PUBLISH_DIR=".ci-cache/publish/social"

mkdir -p "${ROOT}/${GATEWAY_PUBLISH_DIR}" "${ROOT}/${USERS_PUBLISH_DIR}" "${ROOT}/${MEDIA_PUBLISH_DIR}" "${ROOT}/${CHAT_PUBLISH_DIR}" "${ROOT}/${LOCATION_PUBLISH_DIR}" "${ROOT}/${COMMUNITY_PUBLISH_DIR}" "${ROOT}/${GROUP_PUBLISH_DIR}" "${ROOT}/${SOCIAL_PUBLISH_DIR}"

require_publish_output() {
  local dir="$1"
  local dll="$2"
  [[ -f "${ROOT}/${dir}/${dll}" ]] || fail "missing publish output: ${dir}/${dll} (run dotnet-publish.sh first)"
}

log_step "BUILD SDK IMAGE"
build_sdk_image tangle-study-sdk:local

log_step "PUBLISH GATEWAY, USERS, MEDIA, CHAT, LOCATION, COMMUNITY, GROUP, AND SOCIAL (${CONFIGURATION})"
nuget_mount="$(ci_nuget_mount)"
tangle_compose --profile tools run --rm \
  -v "$nuget_mount" \
  --entrypoint bash \
  sdk -c "
    set -euo pipefail
    dotnet publish services/Gateway/Gateway.csproj \
      -c '${CONFIGURATION}' \
      -o '${GATEWAY_PUBLISH_DIR}' \
      /p:UseAppHost=false
    dotnet publish services/Users/Users.csproj \
      -c '${CONFIGURATION}' \
      -o '${USERS_PUBLISH_DIR}' \
      /p:UseAppHost=false
    dotnet publish services/Media/Media.csproj \
      -c '${CONFIGURATION}' \
      -o '${MEDIA_PUBLISH_DIR}' \
      /p:UseAppHost=false
    dotnet publish services/Chat/Chat.csproj \
      -c '${CONFIGURATION}' \
      -o '${CHAT_PUBLISH_DIR}' \
      /p:UseAppHost=false
    dotnet publish services/Location/Location.csproj \
      -c '${CONFIGURATION}' \
      -o '${LOCATION_PUBLISH_DIR}' \
      /p:UseAppHost=false
    dotnet publish services/Community/Community.csproj \
      -c '${CONFIGURATION}' \
      -o '${COMMUNITY_PUBLISH_DIR}' \
      /p:UseAppHost=false
    dotnet publish services/Group/Group.csproj \
      -c '${CONFIGURATION}' \
      -o '${GROUP_PUBLISH_DIR}' \
      /p:UseAppHost=false
    dotnet publish services/Social/Social.csproj \
      -c '${CONFIGURATION}' \
      -o '${SOCIAL_PUBLISH_DIR}' \
      /p:UseAppHost=false
    dotnet build services/Users.Tests/Users.Tests.csproj -c '${CONFIGURATION}' --no-incremental
    dotnet build services/Media.Tests/Media.Tests.csproj -c '${CONFIGURATION}' --no-incremental
    dotnet build services/Chat.Tests/Chat.Tests.csproj -c '${CONFIGURATION}' --no-incremental
    dotnet build services/Location.Tests/Location.Tests.csproj -c '${CONFIGURATION}' --no-incremental
    dotnet build services/Community.Tests/Community.Tests.csproj -c '${CONFIGURATION}' --no-incremental
    dotnet build services/Group.Tests/Group.Tests.csproj -c '${CONFIGURATION}' --no-incremental
    dotnet build services/Social.Tests/Social.Tests.csproj -c '${CONFIGURATION}' --no-incremental
    dotnet build services/Gateway.Tests/Gateway.Tests.csproj -c '${CONFIGURATION}' --no-incremental
    dotnet build services/Stack.Tests/Stack.Tests.csproj -c '${CONFIGURATION}' --no-incremental
  "

require_publish_output "$GATEWAY_PUBLISH_DIR" "Gateway.dll"
require_publish_output "$USERS_PUBLISH_DIR" "Users.dll"
require_publish_output "$MEDIA_PUBLISH_DIR" "Media.dll"
require_publish_output "$CHAT_PUBLISH_DIR" "Chat.dll"
require_publish_output "$LOCATION_PUBLISH_DIR" "Location.dll"
require_publish_output "$COMMUNITY_PUBLISH_DIR" "Community.dll"
require_publish_output "$GROUP_PUBLISH_DIR" "Group.dll"
require_publish_output "$SOCIAL_PUBLISH_DIR" "Social.dll"

ci_fix_cache_ownership

log_info "dotnet publish completed (${CONFIGURATION})"
