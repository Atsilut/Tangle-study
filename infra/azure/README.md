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

`usePlaceholderImages: true` allows infra deploy before custom images exist —
it swaps `api`/`web`/`worker` images for a public placeholder and sets
`targetPort: 80` (matching the placeholder's exposed port) instead of the
real app port (`8080`). Prometheus/Grafana always use their upstream images
regardless of this flag.

**This is a one-time bootstrap switch, not a steady-state setting.** It only
matters for the manual `azure-deploy-infra.sh` path (see
[CD vs manual deploy](#cd-vs-manual-deploy) below) — once the stack has been
bootstrapped and real images exist in GHCR, set it to `false` and leave it
there. Routine CD deploys never read this flag at all.

## Internal networking

Use **internal FQDNs** (`tangle-study-<app>.internal.<cae-default-domain>`) for Redis and scrape targets — short names are unreliable for ACA TCP routing.

### ACA HTTP short names: do not append `targetPort`

Within a Container Apps Environment, HTTP ingress apps can be reached by **short app name**
(e.g. `tangle-study-api`). This behaves differently from Docker Compose, where `api:8080`
works because the service listens on that port directly.

On ACA, the short name resolves to the **ingress front door** (port **80**), which forwards
to the container's `targetPort` (8080 for our API). If you append `:8080` to the short name,
the client connects to the **pod IP on port 8080** instead of the ingress — and that port is
not exposed on the pod network, so the connection **times out**.

| Caller URL | Result |
|------------|--------|
| `http://tangle-study-api/health` | Works — ingress :80 → container :8080 |
| `http://tangle-study-api:8080/health` | **Timeout** — hits pod IP :8080 directly |
| `https://tangle-study-api.internal.<domain>/health` | Works — internal FQDN on :443 |

**Symptoms when misconfigured** (`TANGLE_API_UPSTREAM=tangle-study-api:8080`):

- Direct curl from web container to `http://tangle-study-api/health` → **200 Healthy**
- Curl through nginx (`http://127.0.0.1/health`) → **504 Gateway Timeout**
- Public smoke test (`https://<web-fqdn>/health`) → **504**
- Nginx error log: `upstream timed out ... while connecting to upstream` to `100.100.x.x:8080`

**Correct config:** set `TANGLE_API_UPSTREAM` to the short name **only** (no port):

```json
"TANGLE_API_UPSTREAM": "tangle-study-api",
"TANGLE_API_HOST": "tangle-study-api"
```

Nginx renders this at container start ([`infra/nginx/docker-entrypoint.sh`](../nginx/docker-entrypoint.sh)).
Changing the env var requires a **new web revision** — a running container keeps the old
upstream until redeployed.

**Verify from the web container** (after exec):

```bash
# Wrong upstream still baked in?
grep -A1 'upstream tangle_api' /etc/nginx/conf.d/default.conf

# Direct to API (bypasses nginx) — should work either way
curl -sf http://tangle-study-api/health

# Through nginx — fails until upstream omits :8080
curl -sv http://127.0.0.1/health
```

**Fix in prod:**

```bash
az containerapp update -n tangle-study-web -g tangle-study-prod \
  --set-env-vars TANGLE_API_UPSTREAM=tangle-study-api TANGLE_API_HOST=tangle-study-api
```

Or re-run CD [`scripts/cd/azure-cd-deploy-image.sh`](../../scripts/cd/azure-cd-deploy-image.sh), which reads
[`parameters.prod.json`](parameters.prod.json).

For **HTTP ingress** apps (API, web→API), use the short Container App name
**without a port** (e.g. `tangle-study-api`). The FQDN requirement below applies specifically
to **TCP ingress** (Redis, Postgres-era scrape targets), where short-name DNS resolution has
been unreliable in practice.

| App | Hostname (within environment) |
|-----|------------------------------|
| Postgres | **Neon** (external; not in ACA) |
| Redis | `tangle-study-redis.internal.<domain>:6379` |
| API | `tangle-study-api` (HTTP ingress short name; port 80 implicit) |
| Prometheus | `tangle-study-prometheus.internal.<domain>:9090` |
| Web | public FQDN → proxies to API |
| Grafana | public FQDN (external ingress) |

Postgres connection string is injected by CD from GitHub secret `POSTGRES_CONNECTION_STRING` (API + migrate job). Postgres-exporter gets a derived `postgresql://` DSN for Neon. Redis URL is Bicep-managed (`redis://tangle-study-redis.internal.<domain>:6379`).

## CD vs manual deploy

Two independent deploy paths exist, and they do **not** share state at
runtime — keeping this straight matters, because an env var set by one path
is invisible to the other.

- **Manual (`azure-deploy-infra.sh` → `main.bicep`)**: one-time bootstrap.
  Provisions the Container Apps Environment, Redis, storage, monitoring,
  and seeds the *initial* container env vars/secrets (including a computed
  `TANGLE_API_UPSTREAM` for web, based on `usePlaceholderImages`). This is
  meant for first-time setup or infra-only changes (module edits), and is
  not re-run automatically by CD.
- **CD (`main` branch / Deploy workflow → `scripts/cd/azure-cd-deploy-image.sh`)**:
  the routine deploy path. Builds and pushes GHCR images, waits for
  propagation, then calls `az containerapp update --set-env-vars` per app
  using **`parameters.prod.json` → `containerApps.<name>.env` as the single
  source of truth**. It does **not** re-run Bicep, so any env var that only
  exists in `main.bicep` — and isn't also listed in
  `parameters.prod.json` — will never be updated by CD and can silently
  drift from what Bicep would compute (this bit us once with
  `TANGLE_API_UPSTREAM` pointing at the wrong port after switching
  `usePlaceholderImages`).

**Practical rule:** if a container app's env var can change (ports,
hostnames, feature flags), define it explicitly in `parameters.prod.json`'s
`containerApps` block, not just in `main.bicep`. Treat `main.bicep`'s
computed env values (like `apiAppUpstream`) as bootstrap defaults only —
`parameters.prod.json` overrides them on every subsequent CD run.

Because CD never re-runs Bicep, a full clean deploy through CD alone is
always safe with `usePlaceholderImages: false` — CD builds and pushes real
images before touching any Container App, regardless of what that flag says.
The flag only matters if you're re-running the manual bootstrap script.

## Monitoring on ACA

| Container App | Ingress | Notes |
|---------------|---------|-------|
| `tangle-study-postgres-exporter` | internal :9187 | Scrapes Neon (`DATA_SOURCE_NAME` from CD) |
| `tangle-study-redis-exporter` | internal :9121 | Scrapes internal Redis |
| `tangle-study-prometheus` | internal :9090 | Scrapes API, workers, exporters |
| `tangle-study-grafana` | **external** :3000 | Login `admin` / `GRAFANA_ADMIN_PASSWORD` |

Grafana bundles dashboards and alerts from [`infra/grafana/provisioning/`](../grafana/provisioning/). See [infra/README.md](../README.md) for metric and alert details.

## After Bicep deploy

> **Clean deploy order matters.** For a brand-new stack, run the manual
> `azure-deploy-infra.sh` bootstrap once (with `usePlaceholderImages: true`
> only if GHCR images don't exist yet) to create the Container Apps
> Environment, Redis, storage, and monitoring shells. After that, all
> routine deploys — including future clean redeploys of app containers —
> go through the **Deploy** GitHub Actions workflow, which builds and
> pushes GHCR images first and then updates each Container App directly
> from `parameters.prod.json` (see [CD vs manual deploy](#cd-vs-manual-deploy)).
> The manual script is not part of this routine loop and should not need
> to be re-run unless infra itself (Bicep modules) changes.

1. Create a Neon project and database; copy the Npgsql connection string.
2. Copy storage account connection string → GitHub secret `BLOB_CONNECTION_STRING`.
3. Set GitHub Environment **`prod`** secrets (see [DEPLOYMENT.md](../../docs/DEPLOYMENT.md)), including `POSTGRES_CONNECTION_STRING` and `GRAFANA_ADMIN_PASSWORD`.
4. Merge to **`main`** (or run **Deploy** workflow) — CD pushes GHCR images and updates Container Apps.
5. Migrate runs automatically via `scripts/cd/azure-cd-migrate.sh` in the deploy workflow.
6. Smoke tests run via `scripts/cd/azure-cd-smoke.sh` (`/health` + SPA shell).

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