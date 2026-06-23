# Azure infrastructure (Bicep)

Bicep templates for Tangle on **Azure Container Apps**, tuned for a **study / low-cost** setup.

## What we avoid (no free tier)

| Service | Alternative |
|---------|-------------|
| Azure Database for PostgreSQL | **`tangle-study-postgres`** Container App (`postgres:18` + Azure File persistence) |
| Azure Cache for Redis | **`tangle-study-redis`** Container App (`redis:8-alpine`) |
| Azure Container Registry (ACR) | **[GitHub Container Registry (GHCR)](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry)** — free for public packages |

## What stays on Azure

| Resource | Why |
|----------|-----|
| Container Apps Environment | Core hosting (consumption / free grant) |
| Storage account | Blob media (cheap; free tier eligible) + Azure File for Postgres data |
| Log Analytics | Container Apps diagnostics (minimal volume for study) |
| Application Insights | API telemetry (workspace-based, free tier eligible) |

## Layout

```
infra/azure/
  main.bicep                 # Per-environment stack
  parameters.dev.json        # Optional local experiments (tangle-study-dev)
  parameters.prod.json       # Production (tangle-study-prod) — CD target
  modules/
    infra-container.bicep    # Postgres + Redis (internal, no ingress)
    environment-storage.bicep
    storage.bicep            # Blob + file share
    app-insights.bicep       # Application Insights (linked to Log Analytics)
    container-app.bicep        # API, web, workers (GHCR pull)
    migrate-job.bicep
    ...
```

## Manual deploy (production)

```bash
chmod +x scripts/azure-deploy-infra.sh

POSTGRES_ADMIN_PASSWORD='your-secure-password' ./scripts/azure-deploy-infra.sh prod
```

Optional private GHCR auth:

```bash
GHCR_REGISTRY_USERNAME='your-github-user' \
GHCR_REGISTRY_PASSWORD='ghp_...' \
POSTGRES_ADMIN_PASSWORD='...' \
./scripts/azure-deploy-infra.sh dev
```

Public GHCR images need **no** registry username/password on Container Apps.

## Images (GHCR)

CD builds and pushes:

- `ghcr.io/<org>/tangle-study/tangle-study-api:<tag>`
- `ghcr.io/<org>/tangle-study/tangle-study-web:<tag>`
- `ghcr.io/<org>/tangle-study/tangle-study-worker:<tag>`

Set `containerRegistry` in `parameters.prod.json` to match your org.

`usePlaceholderImages: true` allows infra deploy before images exist.

## Internal networking

| App | Hostname (within environment) |
|-----|------------------------------|
| Postgres | `tangle-study-postgres:5432` |
| Redis | `tangle-study-redis:6379` |
| API | `tangle-study-api:8080` (internal ingress) |
| Web | public FQDN → proxies to API |

Postgres password is injected by CD from GitHub secret `POSTGRES_ADMIN_PASSWORD` (API, migrate job, and postgres container). Use the same value at infra deploy for first-time Postgres init. Redis URL is plain env (`redis://tangle-study-redis:6379`).

## After Bicep deploy

1. Copy storage connection string → GitHub secret `BLOB_CONNECTION_STRING`.
2. Set GitHub Environment **`production`** secrets (see [DEPLOYMENT.md](../../docs/DEPLOYMENT.md)), including `POSTGRES_ADMIN_PASSWORD` matching infra deploy.
3. Merge to **`main`** (or run **Deploy** workflow) — CD pushes GHCR images and updates Container Apps.
4. Migrate runs automatically via `scripts/azure-cd-migrate.sh` in the deploy workflow.
5. Smoke tests run via `scripts/azure-cd-smoke.sh` (`/health` + SPA shell).

## Resource groups

| Group | Purpose |
|-------|---------|
| `tangle-study-dev` | Optional local / experiment stack |
| `tangle-study-prod` | **Production** — GitHub Environment `production`, CD on `main` |

## Trade-offs (study project)

- Container Postgres/Redis share ACA compute — fine for learning, not production HA.
- Postgres data persists on Azure File; Redis is ephemeral (acceptable for cache/streams in dev).
- Scale-to-zero workers (`workerMinReplicas: 0`) save cost; Postgres/Redis stay at `minReplicas: 1`.
