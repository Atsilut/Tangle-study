# Docker image versions

Compose project name: **`tangle-study`** (containers, volumes, and networks are prefixed accordingly).

Third-party image tags are parameterized in [`docker-compose.yml`](../docker-compose.yml). Defaults use **latest / floating** tags for local development.

| File | Purpose |
|------|---------|
| [`versions.dev.env`](versions.dev.env) | Explicit latest tags (optional; same as compose defaults) |
| [`versions.prod.env`](versions.prod.env) | **Pinned** tags for CI, Azure deployment, and prod-like runs |

## Local development (latest)

```bash
docker compose up --build
# or explicitly:
docker compose --env-file docker/versions.dev.env up --build
```

## CI / deployment (pinned)

```bash
docker compose --env-file docker/versions.prod.env up --build
```

GitHub Actions and `./scripts/run-all-tests.sh` set `COMPOSE_ENV_FILE=docker/versions.prod.env` for reproducible builds.

When bumping pins, update **only** `versions.prod.env` and verify with `./scripts/run-all-tests.sh`.

Azure Container Apps deploy builds load the same build-args from `versions.prod.env` via [`scripts/azure-cd-build-push.sh`](../scripts/azure-cd-build-push.sh). Infra deploy passes Redis and monitoring image tags from the same file ([`scripts/azure-deploy-infra.sh`](../scripts/azure-deploy-infra.sh)). Postgres is external (Neon) — not provisioned in Bicep.
