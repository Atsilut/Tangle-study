# Docker images and versions

Container image tags for local Compose, CI, and deployment.

## Dev vs pinned

| Mode | Tags | How |
|------|------|-----|
| **Local dev** (default) | `latest` (or image defaults in `docker-compose.yml`) | `docker compose up --build` |
| **CI / prod-like** | Pinned in [`versions.prod.env`](versions.prod.env) | `docker compose --env-file docker/versions.prod.env up --build` |

`versions.prod.env` is **generated** from [`infra/azure/parameters.prod.json`](../infra/azure/parameters.prod.json). Do not edit it by hand.

```bash
# Regenerate pinned env
bash scripts/ci/render-versions-env.sh docker/versions.prod.env
```

## `COMPOSE_ENV_FILE`

CI scripts and helpers honor `COMPOSE_ENV_FILE` when set (default for CI: `docker/versions.prod.env`):

```bash
export COMPOSE_ENV_FILE=docker/versions.prod.env
./scripts/ci/dotnet-publish.sh
./scripts/ci/docker-test.sh
```

Shared helper: [`scripts/shared/compose-env.sh`](../scripts/shared/compose-env.sh) (`tangle_compose` prepends `--env-file` when the file exists).

## SDK image

[`Dockerfile.sdk`](Dockerfile.sdk) is the shared .NET SDK image for build, EF, and tests (`sdk` / `test` / `harness` Compose services). Restore includes Api, Media, Chat, Location and their test projects.

## Containerized .NET workflow

Do **not** run `dotnet` on the host (avoids `.nuget/` / `.dotnet-tools/` in the repo).

| Task | Script |
|------|--------|
| Build / EF / CLI | [`scripts/docker-dotnet.sh`](../scripts/docker-dotnet.sh) |
| Integration tests (Testcontainers) | [`scripts/ci/docker-test.sh`](../scripts/ci/docker-test.sh) |
| Publish for runtime images | [`scripts/ci/dotnet-publish.sh`](../scripts/ci/dotnet-publish.sh) |

Full service table and ports: [README.md](../README.md#local-development-docker-only). Runtime-only images for CI/CD: [`docker-compose.runtime.yml`](../docker-compose.runtime.yml).
