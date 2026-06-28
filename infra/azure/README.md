# Azure infrastructure (Bicep)

Bicep templates for Tangle on **Azure Container Apps**, tuned for a **study / low-cost** setup.

## What we avoid (no free tier)

| Service | Alternative |
|---------|-------------|
| Azure Database for PostgreSQL | **[Neon](https://neon.tech)** — external Postgres via `POSTGRES_CONNECTION_STRING` GitHub secret ([why not Postgres on ACA](#why-not-postgres-on-aca)) |
| Azure Cache for Redis | **`tangle-study-redis`** Container App (`redis:8-alpine`) |
| Azure Container Registry (ACR) | **[GitHub Container Registry (GHCR)](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry)** — free for public packages |

## What stays on Azure

| Resource | Why |
|----------|-----|
| Container Apps Environment | Core hosting (consumption / free grant) |
| Storage account | Blob media (cheap; free tier eligible) |
| Log Analytics | Container Apps diagnostics (minimal volume for study) |
| Application Insights | API telemetry (workspace-based, free tier eligible) |
| Monitoring Container Apps | Prometheus, Grafana, postgres/redis exporters (Compose parity) |

## Layout

```
infra/azure/
  main.bicep                 # Per-environment stack
  parameters.dev.json        # Optional local experiments (tangle-study-dev)
  parameters.prod.json       # Production (tangle-study-prod) — CD target
  monitoring/
    prometheus/              # Custom GHCR image (ACA scrape config)
    grafana/                 # Custom GHCR image (bundles infra/grafana provisioning)
  modules/
    infra-container.bicep    # Redis (internal TCP ingress on :6379)
    storage.bicep            # Blob only
    app-insights.bicep       # Application Insights (linked to Log Analytics)
    container-app.bicep      # API, web, workers, monitoring (GHCR pull)
    migrate-job.bicep
    ...
```

## Manual deploy (production)

```bash
chmod +x scripts/azure-deploy-infra.sh
./scripts/azure-deploy-infra.sh prod
```

Optional private GHCR auth:

```bash
GHCR_REGISTRY_USERNAME='your-github-user' \
GHCR_REGISTRY_PASSWORD='ghp_...' \
./scripts/azure-deploy-infra.sh dev
```

Public GHCR images need **no** registry username/password on Container Apps.

## Images (GHCR)

CD builds and pushes:

- `ghcr.io/<org>/tangle-study/tangle-study-api:<tag>`
- `ghcr.io/<org>/tangle-study/tangle-study-web:<tag>`
- `ghcr.io/<org>/tangle-study/tangle-study-worker:<tag>`
- `ghcr.io/<org>/tangle-study/tangle-study-prometheus:<tag>`
- `ghcr.io/<org>/tangle-study/tangle-study-grafana:<tag>`

Exporter images (`postgres-exporter`, `redis-exporter`) use public Docker Hub tags from `docker/versions.prod.env` via Bicep parameters.

Set `containerRegistry` in `parameters.prod.json` to match your org.

`usePlaceholderImages: true` allows infra deploy before custom images exist (Prometheus/Grafana use upstream images until CD pushes custom builds).

## Internal networking

Use **internal FQDNs** (`tangle-study-<app>.internal.<cae-default-domain>`) for Redis and scrape targets — short names are unreliable for ACA TCP routing.

| App | Hostname (within environment) |
|-----|------------------------------|
| Postgres | **Neon** (external; not in ACA) |
| Redis | `tangle-study-redis.internal.<domain>:6379` |
| API | `tangle-study-api.internal.<domain>:8080` |
| Prometheus | `tangle-study-prometheus.internal.<domain>:9090` |
| Web | public FQDN → proxies to API |
| Grafana | public FQDN (external ingress) |

Postgres connection string is injected by CD from GitHub secret `POSTGRES_CONNECTION_STRING` (API + migrate job). Postgres-exporter gets a derived `postgresql://` DSN for Neon. Redis URL is Bicep-managed (`redis://tangle-study-redis.internal.<domain>:6379`).

## Monitoring on ACA

| Container App | Ingress | Notes |
|---------------|---------|-------|
| `tangle-study-postgres-exporter` | internal :9187 | Scrapes Neon (`DATA_SOURCE_NAME` from CD) |
| `tangle-study-redis-exporter` | internal :9121 | Scrapes internal Redis |
| `tangle-study-prometheus` | internal :9090 | Scrapes API, workers, exporters |
| `tangle-study-grafana` | **external** :3000 | Login `admin` / `GRAFANA_ADMIN_PASSWORD` |

Grafana bundles dashboards and alerts from [`infra/grafana/provisioning/`](../grafana/provisioning/). See [infra/README.md](../README.md) for metric and alert details.

## After Bicep deploy

1. Create a Neon project and database; copy the Npgsql connection string.
2. Copy storage account connection string → GitHub secret `BLOB_CONNECTION_STRING`.
3. Set GitHub Environment **`prod`** secrets (see [DEPLOYMENT.md](../../docs/DEPLOYMENT.md)), including `POSTGRES_CONNECTION_STRING` and `GRAFANA_ADMIN_PASSWORD`.
4. Merge to **`main`** (or run **Deploy** workflow) — CD pushes GHCR images and updates Container Apps.
5. Migrate runs automatically via `scripts/azure-cd-migrate.sh` in the deploy workflow.
6. Smoke tests run via `scripts/azure-cd-smoke.sh` (`/health` + SPA shell).

If upgrading from an older stack with `tangle-study-postgres`, delete the orphaned Container App and `postgres-data` file share after redeploying Bicep.

## Resource groups

| Group | Purpose |
|-------|---------|
| `tangle-study-dev` | Optional local / experiment stack |
| `tangle-study-prod` | **Production** — GitHub Environment `prod`, CD on `main` |

## Redis on ACA (trade-offs)

API and workers are **stateless**; Redis holds shared ephemeral state (cache, SignalR backplane, Streams, live positions). This is the correct pattern for multi-replica ACA.

| Risk | Impact |
|------|--------|
| No persistence volume on ACA Redis | Restart wipes cache/streams/live positions; Neon is source of truth |
| Single replica SPOF | Redis down → API `/health` fails, queues/SignalR stop |
| Workers scale to 0 | Streams backlog while cold; data lost only if Redis also restarts |
| No TLS on internal Redis | OK within CAE; revisit if moving to managed Redis |

Scale-to-zero workers (`workerMinReplicas: 0`) save cost; Redis and monitoring stay at `minReplicas: 1`.

## Why not Postgres on ACA

Azure Container Apps is a good fit for **stateless** app containers. The first production stack mirrored local Compose: a `tangle-study-postgres` Container App with an Azure File `postgres-data` volume. That experiment proved unreliable in production CD, so Postgres moved to **Neon** (external managed database).

| Problem | What happened |
|---------|----------------|
| **Stateful DB on a stateless platform** | Postgres required `environment-storage` + Azure File mount (`postgres-data`), uid/gid mount options, and a single replica — opposite of how ACA is meant to be operated. |
| **Slow / fragile first boot** | `initdb` on Azure Files routinely took **15–30 minutes**; CD had to wait on log line `database system is ready to accept connections`, not just revision state. |
| **False "healthy" signals** | In-container TCP probes on `:5432` could pass while **other Container Apps** (migrate job, API) still got TCP timeouts to `tangle-study-postgres.internal.<domain>:5432`. |
| **Unreliable ACA service discovery** | Short hostnames (`tangle-study-postgres`) were unreliable for TCP; internal FQDN was required (same class of issue as Redis — see [azure-container-apps#1315](https://github.com/microsoft/azure-container-apps/issues/1315)). |
| **Postgres listen binding** | Default `listen_addresses` did not accept cross-app connections; required `listen_addresses=*` and long startup probes, plus CD backfills on live stacks. |
| **Password vs data directory** | Azure Files retains initialized data; changing `POSTGRES_ADMIN_PASSWORD` in GitHub without resetting the volume caused `password authentication failed`. |
| **CD complexity spiral** | Mitigations stacked up: `wait-for-postgres` init containers, `ensure-infra` patches, `--db-check` cross-app gate before migrate, and a dedicated connectivity diagnose script — high maintenance for a study project. |

For a learning deployment with no data-migration requirement, **Neon** (free tier, SSL, no ACA networking) replaces self-hosted Postgres. Redis remains on ACA because it holds **ephemeral** state only; Postgres is the **source of truth** and belongs in managed storage.

**Current wiring:** GitHub secret `POSTGRES_CONNECTION_STRING` → CD inject into API and migrate job; `tangle-study-postgres-exporter` scrapes Neon via a derived DSN (see [Monitoring on ACA](#monitoring-on-aca) above).

**Upgrading from an old stack:** delete orphaned `tangle-study-postgres` Container App and `postgres-data` file share after redeploying Bicep (see [After Bicep deploy](#after-bicep-deploy)).
