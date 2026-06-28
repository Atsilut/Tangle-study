# Deployment

Azure Container Apps deployment for the Tangle monolith. Secrets are injected from **GitHub Environment secrets** at deploy time via [`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml).

Local development continues to use Docker Compose — see [README](../README.md).

---

## Environments

| Environment | GitHub Environment | Trigger |
|-------------|-------------------|---------|
| production | `production` | CI success on `main`, or manual `workflow_dispatch` |

`develop` runs CI only — no automatic deploy. Local experiments may use `tangle-study-dev` via `./scripts/azure-deploy-infra.sh dev`.

Set `ASPNETCORE_ENVIRONMENT=Production` on the API Container App so [`appsettings.Production.json`](../services/Api/appsettings.Production.json) loads.

---

## Container image versions

Build and deploy use **pinned** base images from [`docker/versions.prod.env`](../docker/versions.prod.env). App images are published to **GHCR** (not ACR). Pass the same build-args when building in CI:

| Build-arg | Prod value (example) |
|-----------|---------------------|
| `DOTNET_SDK_IMAGE` / `DOTNET_ASPNET_IMAGE` | `mcr.microsoft.com/dotnet/sdk:10.0` |
| `NODE_IMAGE` / `NGINX_IMAGE` | `node:26.3-bookworm-slim`, `nginx:1.31.1-alpine` |
| `RUST_IMAGE` / `DEBIAN_IMAGE` | `rust:1.96.0-bookworm`, `debian:bookworm-slim` |

On Azure, **Postgres is [Neon](https://neon.tech)** (external, SSL). **Redis** runs as an internal Container App. **Prometheus + Grafana** run on ACA for metrics (Compose parity). Blob storage uses a standard Storage account. See [`infra/azure/README.md`](../infra/azure/README.md).

See [`docker/README.md`](../docker/README.md).

---

## Azure infrastructure (Bicep)

Study-friendly stack — **no managed Redis or ACR** on Azure (no useful free tier). Postgres uses **Neon** (free tier). Templates: [`infra/azure/`](../infra/azure/).

| Compose (local) | Azure |
|-----------------|-------|
| `db` | **Neon** (external Postgres) |
| `redis` | `tangle-study-redis` Container App |
| `prometheus` / `grafana` / exporters | Monitoring Container Apps on ACA |
| `azurite` | Storage account (blob) |
| Built images | **GHCR** (`ghcr.io/<org>/tangle-study/...`) |

Postgres is external because self-hosted Postgres on ACA proved unreliable (cross-app TCP, Azure Files boot times, false health). See [Why not Postgres on ACA](../infra/azure/README.md#why-not-postgres-on-aca).

```bash
./scripts/azure-deploy-infra.sh prod
```

Bicep sets Redis URL and monitoring hostnames from internal ACA FQDNs. **App secrets** (Neon connection string, JWT, blob, Grafana, etc.) are injected after deploy via GitHub Actions. Details: [`infra/azure/README.md`](../infra/azure/README.md).

---

## GitHub setup (one-time)

### 1. Azure OIDC federated credential

Create an app registration and federated credential for GitHub Actions ([Microsoft docs](https://learn.microsoft.com/en-us/azure/developer/github/connect-from-azure-openid-connect)). Use subject `repo:<owner>/<repo>:environment:production`. Grant **Contributor** on resource group `tangle-study-prod`.

### 2. GitHub Environment `production`

**Variables:**

| Variable | Example |
|----------|---------|
| `AZURE_CLIENT_ID` | App registration client ID |
| `AZURE_TENANT_ID` | Azure AD tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Subscription GUID |
| `AZURE_RESOURCE_GROUP` | `tangle-study-prod` |
| `CONTAINER_REGISTRY` | `ghcr.io/your-org/tangle-study` (optional; defaults from repo owner) |

**Secrets:**

| Secret | Required | Notes |
|--------|----------|-------|
| `BLOB_CONNECTION_STRING` | Yes | From Azure Storage account (created by Bicep) |
| `JWT_SECRET` | Yes | Min 32 chars |
| `WORKER_CALLBACK_SECRET` | Yes | Shared with media worker |
| `METRICS_SCRAPE_SECRET` | Yes | Random string; API, Prometheus, and workers |
| `POSTGRES_CONNECTION_STRING` | Yes | Neon connection string — Npgsql (`Host=...;Database=...;Username=...;Password=...;SSL Mode=Require`) or URI (`postgresql://user:pass@host/db?sslmode=require`) from the Neon console |
| `GRAFANA_ADMIN_PASSWORD` | Yes | Grafana admin login on external ACA app |
| `PLACES_API_KEY` | No | Google Places / Geocoding |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | No | Auto-fetched from Azure App Insights when unset |
| `GHCR_REGISTRY_USERNAME` | No | Only for **private** GHCR packages |
| `GHCR_REGISTRY_PASSWORD` | No | GitHub PAT with `read:packages` |

Use Neon **direct** (non-pooler) hostname for API and migrate at study scale. Either format works (Npgsql accepts both):

`Host=ep-....neon.tech;Database=neondb;Username=...;Password=...;SSL Mode=Require;Pooling=true`

`postgresql://user:password@ep-....neon.tech/neondb?sslmode=require`

### 3. Provision infra (once)

```bash
./scripts/azure-deploy-infra.sh prod
```

Create a Neon project + database and set `POSTGRES_CONNECTION_STRING` in GitHub. Copy the storage account connection string into GitHub secret `BLOB_CONNECTION_STRING`.

### 4. CD pipeline

After CI passes on **`main`**, [deploy.yml](../.github/workflows/deploy.yml):

1. Builds and pushes `tangle-study-api`, `tangle-study-web`, `tangle-study-worker`, `tangle-study-prometheus`, and `tangle-study-grafana` to GHCR
2. Injects secrets into Container Apps (Neon, monitoring, JWT, blob, etc.)
3. Ensures **Redis internal TCP ingress** and `Redis__ConnectionString` / worker `REDIS_URL` ([`azure-cd-ensure-redis.sh`](../scripts/azure-cd-ensure-redis.sh))
4. Updates app images to the commit SHA
5. Runs `tangle-study-migrate` job

Manual deploy: **Actions → Deploy → Run workflow** (uses `production` environment).

Optional: add **required reviewers** on the `production` environment in GitHub for approval gates.

---

## Configuration sources

| Layer | Purpose |
|-------|---------|
| `appsettings.json` | Base defaults |
| `appsettings.Production.json` | Production flags (Redis on, media on, metrics auth on); empty connection strings |
| `security.yml` | JWT issuer/audience/expiry; placeholder secret allowed only in Development/Docker |
| `media-limits.yml` | Upload size limits |
| **Environment variables** | Secrets and connection strings from GitHub Actions → Container Apps |

ASP.NET Core binds nested config with double underscores, e.g. `Redis__ConnectionString` → `Redis:ConnectionString`.

**Important:** Environment variables are re-applied after YAML in [`Program.cs`](../services/Api/Program.cs) so GitHub-injected secrets override `security.yml` placeholders.

---

## GitHub Environment secrets

Store these on GitHub Environment **`production`**. The deploy workflow maps each secret to a Container App secret ref, then to the env vars below.

### API (`services/Api`)

| GitHub secret | Container App env var | Required | Notes |
|---------------|----------------------|----------|-------|
| `BLOB_CONNECTION_STRING` | `Media__ConnectionString` | Yes | Azure Storage account connection string |
| `JWT_SECRET` | `Jwt__Secret` | Yes | Min 32 chars; overrides `security.yml` placeholder |
| `WORKER_CALLBACK_SECRET` | `Media__WorkerCallbackSecret` | Yes | Shared with media worker for internal callbacks |
| `METRICS_SCRAPE_SECRET` | `Metrics__ScrapeSecret` (API), `METRICS_SCRAPE_SECRET` (Prometheus + workers) | Yes | Required when `Metrics:RequireScrapeSecret` is true |
| `POSTGRES_CONNECTION_STRING` | `ConnectionStrings__DefaultConnection` (API + migrate) | Yes | Neon Npgsql connection string from console |
| `GRAFANA_ADMIN_PASSWORD` | `GF_SECURITY_ADMIN_PASSWORD` (Grafana) | Yes | External Grafana Container App admin password |
| `PLACES_API_KEY` | `Places__ApiKey` | No | Google Places / Geocoding; leave empty to disable search |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | `APPLICATIONINSIGHTS_CONNECTION_STRING` | No | Auto-resolved from `tanglestudyprod-appi` when unset |
| `GHCR_REGISTRY_USERNAME` | (registry pull on all apps + migrate job) | No | Only if GHCR packages are **private** |
| `GHCR_REGISTRY_PASSWORD` | (registry pull) | No | GitHub PAT with `read:packages` |

**Not GitHub secrets:** Redis host is set by Bicep (`tangle-study-redis.internal.<cae-domain>:6379` on API; `redis://…` on workers). Postgres-exporter DSN is derived from `POSTGRES_CONNECTION_STRING` at CD time. Blob public endpoint and media container name are non-secret Bicep outputs.

Non-secret config can be GitHub **variables** or Bicep parameters:

| Variable | Container App env var | Example |
|----------|----------------------|---------|
| `MEDIA_PUBLIC_BLOB_ENDPOINT` | `Media__PublicBlobEndpoint` | `https://tanglestaging.blob.core.windows.net` |
| `REDIS_INSTANCE_NAME` | `Redis__InstanceName` | `tangle:` (default) |

### Web (`clients/web` nginx)

| Variable | Env var | Default | Notes |
|----------|---------|---------|-------|
| — | `TANGLE_API_UPSTREAM` | `api:8080` | Internal API host:port within the Container Apps environment |

Build the web image with `--build-arg NGINX_CONF=nginx.production.conf` for Azure (no Azurite proxy). See [`infra/nginx/nginx.production.conf`](../infra/nginx/nginx.production.conf).

### Rust workers (`workers/rust-worker`)

| GitHub secret | Env var | Required | Notes |
|---------------|---------|----------|-------|
| `BLOB_CONNECTION_STRING` | `AZURE_STORAGE_CONNECTION_STRING` | Media worker only | Same storage account as API |
| `WORKER_CALLBACK_SECRET` | `WORKER_CALLBACK_SECRET` | Media worker only | Must match `Media__WorkerCallbackSecret` |
| `METRICS_SCRAPE_SECRET` | `METRICS_SCRAPE_SECRET` | Yes | Protects `/metrics`; Prometheus sends `X-Metrics-Secret` |

Redis URL is set by Bicep (`REDIS_URL=redis://tangle-study-redis.internal.<cae-domain>:6379`).

Per-worker settings (Bicep or GitHub variables):

| Variable | Env var | Example |
|----------|---------|---------|
| `API_BASE_URL` | `API_BASE_URL` | `http://api:8080` (internal) |
| `WORKER_STREAM_KEY` | `WORKER_STREAM_KEY` | `media.uploaded`, `chat.message.created`, `location.cluster` |
| `MEDIA_CONTAINER_NAME` | `MEDIA_CONTAINER_NAME` | `tangle-media` |

---

## Database migrations

Production and staging **do not** apply migrations on API startup. Migrations run as a separate step before rolling out a new API revision.

### Command

```bash
dotnet Api.dll --migrate
```

Implemented in [`DatabaseMigrationRunner.cs`](../services/Api/Global/Db/DatabaseMigrationRunner.cs). Requires `ConnectionStrings__DefaultConnection` (or `appsettings.Production.json` + env override).

### Local (Compose Postgres)

```bash
./scripts/migrate.sh
```

Uses the `api` image against the compose `db` service (`ASPNETCORE_ENVIRONMENT=Docker`).

### Staging / production

Run before deploying a new API revision:

```bash
ConnectionStrings__DefaultConnection="$POSTGRES_CONNECTION_STRING" \
  ./scripts/migrate.sh --production
```

### CD step (Container Apps Job)

The deploy workflow runs migrations automatically before the new API revision serves traffic:

1. Build and push API image to GHCR (same tag as app deploy).
2. Update migrate job image, then `az containerapp job start` on `tangle-study-migrate`.
3. Proceed only if the job execution status is `Succeeded`.

Skip on manual runs: **Deploy → Run workflow → Skip EF migrate job**.

Development/Docker still auto-migrate on API startup for local convenience.

---

## Observability

### Application Insights

Bicep provisions workspace-based **Application Insights** (free tier eligible) and sets `APPLICATIONINSIGHTS_CONNECTION_STRING` on `tangle-study-api`. The API enables [Azure Monitor OpenTelemetry](https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-enable?tabs=aspnetcore) when that variable is present — see [`AzureMonitorTelemetryExtensions.cs`](../services/Api/Global/Telemetry/AzureMonitorTelemetryExtensions.cs).

Container Apps platform logs go to the same Log Analytics workspace.

### Prometheus + Grafana on ACA

Bicep deploys a **monitoring stack** on Container Apps (Compose parity):

| App | Access | Notes |
|-----|--------|-------|
| `tangle-study-grafana` | External HTTPS FQDN | `admin` / `GRAFANA_ADMIN_PASSWORD` |
| `tangle-study-prometheus` | Internal only | Scrapes API, workers, postgres/redis exporters |
| `tangle-study-postgres-exporter` | Internal | Connects to Neon |
| `tangle-study-redis-exporter` | Internal | Scrapes internal Redis |

Resolve Grafana URL after infra deploy:

```bash
az containerapp show --name tangle-study-grafana --resource-group tangle-study-prod \
  --query properties.configuration.ingress.fqdn -o tsv
```

Dashboards, recording rules, and alerts match local Compose — see [`infra/README.md`](../infra/README.md). Workers require `X-Metrics-Secret` on `/metrics` when `METRICS_SCRAPE_SECRET` is set (same as API).

Local Prometheus/Grafana under [`infra/`](../infra/) remain for Docker Compose (`--profile monitoring`).

### Post-deploy smoke tests

After migrate, [deploy.yml](../.github/workflows/deploy.yml) runs [`scripts/azure-cd-smoke.sh`](../scripts/azure-cd-smoke.sh):

- `GET https://<tangle-study-web-fqdn>/health` — expects `Healthy` (proxied to API)
- `GET https://<tangle-study-web-fqdn>/` — SPA shell loads

Manual run:

```bash
AZURE_RESOURCE_GROUP=tangle-study-prod ./scripts/azure-cd-smoke.sh
```

---

## Production E2E validation (before MSA)

Complete this checklist **manually in production** after the first successful CD run. It is the gate for [Phase 9 service extraction](MSA_MIGRATION.md): do not start MSA until every required item passes.

**Web URL:** resolve the public FQDN once:

```bash
az containerapp show --name tangle-study-web --resource-group tangle-study-prod \
  --query properties.configuration.ingress.fqdn -o tsv
```

Open `https://<fqdn>/` in a browser. Use two test accounts (User A and User B) for social features.

Automated smoke tests ([`azure-cd-smoke.sh`](../scripts/azure-cd-smoke.sh)) only cover `/health` and the SPA shell — they do not replace this checklist.

### Pre-flight

- [ ] CD workflow succeeded (build → secrets → image update → migrate → smoke)
- [ ] `GET https://<fqdn>/health` returns `Healthy`
- [ ] Blob CORS allows `PUT` from the web origin (required for browser uploads)
- [ ] `Media__PublicBlobEndpoint` points at the HTTPS blob endpoint clients use for SAS uploads
- [ ] Application Insights shows incoming requests after browsing the app (Azure portal → `tanglestudyprod-appi`)

### Auth & users

- [ ] Register a new account (`/register`)
- [ ] Log in and reach the home page (`/login`)
- [ ] Session persists across refresh (JWT in local storage)
- [ ] View another user's profile (`/users/:id`)
- [ ] Log out; protected routes redirect to `/login`

### Posts & comments

- [ ] Create a text post (`/posts/new`)
- [ ] View post detail; add a comment
- [ ] Edit and delete own post/comment
- [ ] Upload an image on a post (media worker processes thumbnail — allow ~30s if workers scaled to zero)

### Friends & blocks

- [ ] Send friend request (User A → User B); accept on B
- [ ] Block a user; verify blocked content is hidden
- [ ] Unblock

### Groups

- [ ] Create a group (`/groups/new`)
- [ ] Invite / accept member
- [ ] Create a board; add a board post with optional media
- [ ] Group chat room appears; send a message

### Chat (SignalR)

- [ ] Open group chat room; message appears in realtime for another member (no manual refresh)
- [ ] Send a chat message with an image attachment (media worker)

### Memory Map & location (Phase 7 gate)

Requires `PLACES_API_KEY` for place search. Workers may cold-start from zero replicas — wait up to ~1 min for cluster jobs.

- [ ] Map loads at `/map` (OpenStreetMap tiles)
- [ ] Place search returns results (if Places API key configured)
- [ ] Double-click to drop a pin; pin visible after refresh
- [ ] Start live location sharing in a group; User B sees User A's marker
- [ ] Stop sharing; marker disappears for User B
- [ ] SOS alert received by group members while sharing (SignalR `/hubs/location`)
- [ ] Zoom out to cluster view (zoom 2–4); clusters appear after location worker runs

### Workers & async paths

Confirm worker Container Apps (`tangle-study-worker-media`, `tangle-study-worker-chat`, `tangle-study-worker-location`) scale up after triggering the feature above. Check execution logs in Log Analytics if a job stalls.

- [ ] Media upload → thumbnail/variant available in UI
- [ ] Chat message → delivered without page reload
- [ ] Location cluster job completes (cluster markers at low zoom)

### Observability

- [ ] API requests visible in Application Insights (Live Metrics or Transactions)
- [ ] Grafana external URL loads; Prometheus targets UP (api, postgres, redis, workers)
- [ ] Container Apps logs stream without repeated crash loops (`az containerapp logs show -n tangle-study-api -g tangle-study-prod --tail 50`)
- [ ] No sustained 5xx on `/api/*` during the walkthrough

### Sign-off

When all required boxes are checked, record the validating commit SHA and date here (or in your project notes):

```
Validated: <YYYY-MM-DD> @ <git-sha> on https://<fqdn>
```

Only then proceed to [MSA extraction](MSA_MIGRATION.md#extraction-order).

---

## Troubleshooting (Redis on ACA)

**Symptom:** API logs show `StackExchange.Redis.ConnectionMultiplexer.Connect` failure at startup; revision crash-loops.

**Cause:** `tangle-study-redis` was deployed **without internal TCP ingress**. The API uses `Redis__ConnectionString=tangle-study-redis.internal.<cae-domain>:6379` (cross-app TCP via ACA Envoy, not the short app name).

**Fix:** CD runs [`scripts/azure-cd-ensure-redis.sh`](../scripts/azure-cd-ensure-redis.sh) before secret injection. Re-run manually:

```bash
AZURE_RESOURCE_GROUP=tangle-study-prod ./scripts/azure-cd-ensure-redis.sh
```

If routing is still stale after ingress was enabled, recycle once:

```bash
INFRA_FORCE_TCP_INGRESS_RECYCLE=1 AZURE_RESOURCE_GROUP=tangle-study-prod \
  ./scripts/azure-cd-ensure-redis.sh
```

Verify:

```bash
az containerapp show -n tangle-study-redis -g tangle-study-prod \
  --query "properties.configuration.ingress.{transport:transport,targetPort:targetPort,fqdn:fqdn}" -o yaml

az containerapp show -n tangle-study-api -g tangle-study-prod \
  --query "properties.template.containers[0].env[?name=='Redis__ConnectionString']" -o yaml
```

Fresh infra deploys pick up TCP ingress from [`infra/azure/modules/infra-container.bicep`](../infra/azure/modules/infra-container.bicep) when `tcpProbePort` is set (6379 for Redis).

---

## Production API checklist

Before first deploy:

1. Set all required GitHub secrets for the target environment (including `POSTGRES_CONNECTION_STRING` and `GRAFANA_ADMIN_PASSWORD`).
2. Set `Media__PublicBlobEndpoint` to the blob account URL clients use for SAS uploads (HTTPS).
3. Configure blob CORS on the storage account for the web app origin (`PUT` from browser).
4. Enable Redis — required for SignalR backplane and work queues when the API scales beyond one replica.
5. Run `./scripts/migrate.sh --production` (or the Container Apps migrate job) before each API deploy — startup does not migrate in Production.

JWT placeholder in [`security.yml`](../services/Api/security.yml) causes startup failure outside Development/Docker unless `Jwt__Secret` is injected.

---

## Image build args

```bash
# Local Compose (default — Azurite proxy in nginx)
docker compose build nginx

# Azure / production edge
docker build -f clients/web/Dockerfile \
  --build-arg NGINX_CONF=nginx.production.conf \
  -t tangle-study-web .
```

---

## Related docs

- [ARCHITECTURE.md](ARCHITECTURE.md) — current monolith layout
- [MSA_MIGRATION.md](MSA_MIGRATION.md) — Phase 9 service extraction (after prod monolith is proven)
