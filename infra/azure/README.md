# Azure infrastructure (Bicep)

Bicep templates for Tangle on **Azure Container Apps**, tuned for a **study / low-cost** setup.

**Single source of truth:** [`parameters.prod.json`](parameters.prod.json) defines every Container App (image, env, secret refs, ingress, replicas) and every EF migrate job. [`main.bicep`](main.bicep) loops over that map; CD ([`cd-v2.yml`](../../.github/workflows/cd-v2.yml)) runs Bicep on every deploy.

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
  main.bicep                 # Data-driven stack (loops containerApps + migrateJobs)
  parameters.dev.json        # Optional local experiments (tangle-study-dev)
  parameters.prod.json       # Production (tangle-study-prod) — CD target / SSoT
  monitoring/
    prometheus/              # Custom GHCR image (ACA scrape config for MSA services)
    grafana/                 # Custom GHCR image (bundles infra/grafana provisioning)
  modules/
    infra-container.bicep    # Redis (internal TCP ingress on :6379)
    storage.bicep            # Blob only
    app-insights.bicep       # Application Insights (linked to Log Analytics)
    container-app.bicep      # Gateway, services, web, workers, monitoring (GHCR pull)
    migrate-job.bicep        # Per-service EF migrate jobs
    ...
```

## Manual deploy (production)

Pass the same secure values CD uses (or leave them empty for placeholder-only bootstrap):

```bash
POSTGRES_CONNECTION_STRING='...' \
BLOB_CONNECTION_STRING='...' \
JWT_SECRET='...' \
WORKER_CALLBACK_SECRET='...' \
METRICS_SCRAPE_SECRET='...' \
GRAFANA_ADMIN_PASSWORD='...' \
GATEWAY_SECRET='...' \
USERS_INTERNAL_SECRET='...' \
MEDIA_INTERNAL_SECRET='...' \
CHAT_INTERNAL_SECRET='...' \
LOCATION_INTERNAL_SECRET='...' \
COMMUNITY_INTERNAL_SECRET='...' \
GROUP_INTERNAL_SECRET='...' \
SOCIAL_INTERNAL_SECRET='...' \
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

- `ghcr.io/<org>/tangle-study/tangle-study-gateway:<tag>`
- `ghcr.io/<org>/tangle-study/tangle-study-users:<tag>`
- `ghcr.io/<org>/tangle-study/tangle-study-media:<tag>`
- `ghcr.io/<org>/tangle-study/tangle-study-chat:<tag>`
- `ghcr.io/<org>/tangle-study/tangle-study-location:<tag>`
- `ghcr.io/<org>/tangle-study/tangle-study-community:<tag>`
- `ghcr.io/<org>/tangle-study/tangle-study-group:<tag>`
- `ghcr.io/<org>/tangle-study/tangle-study-social:<tag>`
- `ghcr.io/<org>/tangle-study/tangle-study-web:<tag>`
- `ghcr.io/<org>/tangle-study/tangle-study-worker-media:<tag>`
- `ghcr.io/<org>/tangle-study/tangle-study-worker-chat:<tag>`
- `ghcr.io/<org>/tangle-study/tangle-study-worker-location:<tag>`
- `ghcr.io/<org>/tangle-study/tangle-study-prometheus:<tag>`
- `ghcr.io/<org>/tangle-study/tangle-study-grafana:<tag>`

Exporter images (`postgres-exporter`, `redis-exporter`) use public Docker Hub tags from `parameters.prod.json` → `infra`.

Set `containerRegistry` in `parameters.prod.json` to match your org.

`usePlaceholderImages: true` allows infra deploy before custom images exist —
it swaps gateway/service/web/worker images for a public placeholder and sets
`targetPort: 80` (matching the placeholder's exposed port) instead of the
real app port (`8080`). Prometheus/Grafana use their upstream images when
this flag is true.

**Bootstrap switch only.** CD (`cd-v2.yml`) always passes `usePlaceholderImages=false`
after pushing real images. Manual `azure-deploy-infra.sh` defaults to `true`.

## Internal networking

Cross-app URLs live in [`parameters.prod.json`](parameters.prod.json) as ACA **short Container App names** (no `targetPort`, no internal FQDN). Bicep applies them on every deploy.

### ACA short names: do not append `targetPort`

Within a Container Apps Environment, HTTP ingress apps can be reached by **short app name**
(e.g. `tangle-study-gateway`). This behaves differently from Docker Compose, where `gateway:8080`
works because the service listens on that port directly.

On ACA, the short name resolves to the **ingress front door** (port **80**), which forwards
to the container's `targetPort` (8080 for .NET services). If you append `:8080` to the short name,
the client connects to the **pod IP on port 8080** instead of the ingress — and that port is
not exposed on the pod network, so the connection **times out**.

| Caller URL | Result |
|------------|--------|
| `http://tangle-study-gateway/health` | Works — ingress :80 → container :8080 |
| `http://tangle-study-gateway:8080/health` | **Timeout** — hits pod IP :8080 directly |
| `https://tangle-study-gateway.internal.<domain>/health` | Works — internal FQDN on :443 |

**Correct web config** (in `parameters.prod.json`):

```json
"TANGLE_API_UPSTREAM": "tangle-study-gateway",
"TANGLE_API_HOST": "tangle-study-gateway"
```

Nginx renders this at container start ([`infra/nginx/docker-entrypoint.sh`](../nginx/docker-entrypoint.sh)).

| App | Hostname (within environment) |
|-----|------------------------------|
| Postgres | **Neon** (external; not in ACA) |
| Redis | `tangle-study-redis` (TCP; port 6379 implicit) |
| Gateway | `tangle-study-gateway` (HTTP ingress; web upstream) |
| Users / Media / Chat / Location / Community / Group / Social | `tangle-study-<service>` |
| Prometheus | `tangle-study-prometheus` |
| Workers / exporters | `tangle-study-worker-*`, `tangle-study-postgres-exporter`, `tangle-study-redis-exporter` |
| Web | public FQDN → proxies to gateway |
| Grafana | public FQDN (external ingress) |

Postgres connection string is a secure Bicep param from GitHub secret `POSTGRES_CONNECTION_STRING` (all DB services + migrate jobs). Postgres-exporter gets a derived `postgresql://` DSN. Redis short names are set in `parameters.prod.json`.

## CD vs manual deploy

Both paths run the **same** `main.bicep` against `parameters.prod.json`. Secrets are `@secure()` Bicep params (never stored in the JSON file).

- **Manual (`azure-deploy-infra.sh`)**: bootstrap or infra-only experiments. Defaults `usePlaceholderImages=true`. Pass secure env vars when you want real secret values.
- **CD (`cd-v2.yml` → [`azure-cd-deploy-bicep.sh`](../../scripts/cd/azure-cd-deploy-bicep.sh))**: routine path. Builds/pushes GHCR images, then `az deployment group create` with `imageTag=<sha>`, `usePlaceholderImages=false`, and GitHub Environment secrets. Then runs per-service migrate jobs and smoke tests.

**Practical rule:** add or change an app only in `parameters.prod.json` (`containerApps` / `migrateJobs`). Do not hardcode new apps in `main.bicep`.

## Monitoring on ACA

| Container App | Ingress | Notes |
|---------------|---------|-------|
| `tangle-study-postgres-exporter` | internal :9187 | Scrapes Neon (`DATA_SOURCE_NAME` from Bicep) |
| `tangle-study-redis-exporter` | internal :9121 | Scrapes internal Redis |
| `tangle-study-prometheus` | internal | Custom GHCR image; scrapes gateway + 7 services + workers + exporters |
| `tangle-study-grafana` | **external** :3000 | Custom GHCR image; login `admin` / `GRAFANA_ADMIN_PASSWORD` |

**CD path:** [`scripts/cd/azure-cd-build-push.sh`](../../scripts/cd/azure-cd-build-push.sh) builds
`tangle-study-prometheus` and `tangle-study-grafana` from [`monitoring/`](monitoring/).
Do **not** deploy vanilla `prom/prometheus` or `grafana/grafana` — they skip the ACA entrypoints and provisioning.

Grafana bundles dashboards and alerts from [`infra/grafana/provisioning/`](../grafana/provisioning/). See [infra/README.md](../README.md) for metric and alert details. Short names must omit `targetPort` — see [ACA short names](#aca-short-names-do-not-append-targetport).

## After Bicep deploy

> For a brand-new stack, run `azure-deploy-infra.sh prod` once (placeholders OK if GHCR images do not exist yet). After that, routine deploys go through **Deploy** (`cd-v2.yml`), which builds images then re-runs Bicep from `parameters.prod.json`.

1. Create a Neon project and database; copy the Npgsql connection string.
2. Copy storage account connection string → GitHub secret `BLOB_CONNECTION_STRING`.
3. Set GitHub Environment **`prod`** secrets (see [DEPLOYMENT.md](../../docs/DEPLOYMENT.md)), including `POSTGRES_CONNECTION_STRING`, `GATEWAY_SECRET`, the seven `*_INTERNAL_SECRET` values (`USERS_INTERNAL_SECRET`, …, `SOCIAL_INTERNAL_SECRET`), and `GRAFANA_ADMIN_PASSWORD`. Remove obsolete `INTERNAL_SERVICE_SECRET` after cutover.
4. Merge to **`main`** (or run **Deploy** workflow) — CD pushes GHCR images and runs Bicep.
5. Migrate runs automatically via `scripts/cd/azure-cd-migrate.sh` (one job per DB-owning service).
6. Smoke tests run via `scripts/cd/azure-cd-smoke.sh` (`/health` via web → gateway + SPA shell).

If upgrading from an older stack with `tangle-study-api` / `tangle-study-migrate`, delete those orphaned resources after the MSA cutover deploy.

## Resource groups

| Group | Purpose |
|-------|---------|
| `tangle-study-dev` | Optional local / experiment stack |
| `tangle-study-prod` | **Production** — GitHub Environment `prod`, CD on `main` |

## Redis on ACA (trade-offs)

API services and workers are **stateless**; Redis holds shared ephemeral state (cache, SignalR backplane, Streams, live positions). This is the correct pattern for multi-replica ACA.

| Risk | Impact |
|------|--------|
| No persistence volume on ACA Redis | Restart wipes cache/streams/live positions; Neon is source of truth |
| Single replica SPOF | Redis down → health checks / queues / SignalR stop |
| Workers scale to 0 | Streams backlog while cold; data lost only if Redis also restarts |
| No TLS on internal Redis | OK within CAE; revisit if moving to managed Redis |

Scale-to-zero workers (`minReplicas: 0` in `parameters.prod.json`) save cost; Redis and monitoring stay at `minReplicas: 1`.

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

**Current wiring:** GitHub secret `POSTGRES_CONNECTION_STRING` → Bicep secure param into all DB services and migrate jobs; `tangle-study-postgres-exporter` scrapes Neon via a derived DSN (see [Monitoring on ACA](#monitoring-on-aca) above).

**Upgrading from an old stack:** delete orphaned `tangle-study-postgres` Container App and `postgres-data` file share after redeploying Bicep (see [After Bicep deploy](#after-bicep-deploy)).
