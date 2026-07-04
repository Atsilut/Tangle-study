# Deployment

Azure Container Apps deployment for the Tangle monolith. Secrets are injected from **GitHub Environment secrets** at deploy time via `[.github/workflows/cd-v1.yml](../.github/workflows/cd-v1.yml)`.

Local development uses Docker Compose with **media, chat, and location extracted** (Nginx strangler). See [README](../README.md) and [MSA_MIGRATION.md](MSA_MIGRATION.md#step-3--location-service-develop-done). **Azure production** still deploys only the monolith for `/api/*` until ACA + `nginx.production.conf` cutover lands.

---

## Local Compose (media, chat, location extracted)

Default `docker compose up` runs `api`, `media`, `chat`, `location`, `nginx`, `db`, `redis`, and `azurite`.

| Path | Routed to | Config |
|------|-----------|--------|
| `/api/media/*`, `/internal/media/*` | `media:8080` | [`infra/nginx/nginx.conf`](../infra/nginx/nginx.conf) |
| `/api/chat/*`, `/internal/chat/*`, `/hubs/chat` | `chat:8080` | Same |
| `/api/location/*`, `/internal/location/*`, `/hubs/location` | `location:8080` | Same |
| Other `/api/*` | `api:8080` | Monolith |

**Api (Docker):** `MediaClient`, `ChatClient`, and `LocationClient` base URLs in [`appsettings.Docker.json`](../services/Api/appsettings.Docker.json) point at the extracted services.

**Media / Chat / Location (Docker):** each service's `appsettings.Docker.json` — Redis on where needed, `Monolith__BaseUrl=http://api:8080`, shared `dev-internal-service-secret` for `X-Internal-Secret`.

**Workers:** `API_BASE_URL=http://media:8080` on `rust-worker-media`, `http://chat:8080` on `rust-worker-chat`, `http://location:8080` on `rust-worker-location`.

**Harness E2E:** `TANGLE_HARNESS_API_BASE_URL=http://nginx` — `./scripts/ci/run-media-harness.sh`.

**Integration tests** (Testcontainers) use in-process fakes / service hosts — no running Compose stack required for Api/Media/Chat/Location unit and integration suites.

---

## Environments


| Environment | GitHub Environment | Trigger                                             |
| ----------- | ------------------ | --------------------------------------------------- |
| production  | `prod`             | CI success on `main`, or manual `workflow_dispatch` |


`develop` runs CI only — no automatic deploy. Local experiments may use `tangle-study-dev` via `./scripts/azure-deploy-infra.sh dev`.

Set `ASPNETCORE_ENVIRONMENT=Production` on the API Container App so `[appsettings.Production.json](../services/Api/appsettings.Production.json)` loads.

---



## Container image versions

Build and deploy use **pinned** base images from `[docker/versions.prod.env](../docker/versions.prod.env)`. App images are published to **GHCR** (not ACR). Pass the same build-args when building in CI:


| Build-arg                                  | Prod value (example)                             |
| ------------------------------------------ | ------------------------------------------------ |
| `DOTNET_SDK_IMAGE` / `DOTNET_ASPNET_IMAGE` | `mcr.microsoft.com/dotnet/sdk:10.0`              |
| `NODE_IMAGE` / `NGINX_IMAGE`               | `node:26.3-bookworm-slim`, `nginx:1.31.1-alpine` |
| `RUST_IMAGE` / `DEBIAN_IMAGE`              | `rust:1.96.0-bookworm`, `debian:bookworm-slim`   |


On Azure, **Postgres is [Neon](https://neon.tech)** (external, SSL). **Redis** runs as an internal Container App. **Prometheus + Grafana** run on ACA for metrics (Compose parity). Blob storage uses a standard Storage account. See `[infra/azure/README.md](../infra/azure/README.md)`.

See `[docker/README.md](../docker/README.md)`.

---



## Azure infrastructure (Bicep)

Study-friendly stack — **no managed Redis or ACR** on Azure (no useful free tier). Postgres uses **Neon** (free tier). Templates: `[infra/azure/](../infra/azure/)`.


| Compose (local)                      | Azure                                       |
| ------------------------------------ | ------------------------------------------- |
| `api`                                | `tangle-study-api` Container App (monolith)   |
| `media`                              | **Not deployed yet** — still in monolith on Azure |
| `nginx` / web                        | `tangle-study-web` Container App            |
| `db`                                 | **Neon** (external Postgres)                |
| `redis`                              | `tangle-study-redis` Container App          |
| `prometheus` / `grafana` / exporters | Monitoring Container Apps on ACA            |
| `azurite`                            | Storage account (blob)                      |
| Built images                         | **GHCR** (`ghcr.io/<org>/tangle-study/...`) |


Postgres is external because self-hosted Postgres on ACA proved unreliable (cross-app TCP, Azure Files boot times, false health). See [Why not Postgres on ACA](../infra/azure/README.md#why-not-postgres-on-aca).

```bash
./scripts/azure-deploy-infra.sh prod
```

Bicep sets Redis URL and monitoring hostnames from internal ACA FQDNs. **App secrets** (Neon connection string, JWT, blob, Grafana, etc.) are injected after deploy via GitHub Actions. Details: `[infra/azure/README.md](../infra/azure/README.md)`.

---



## GitHub setup (one-time)



### 1. Azure OIDC federated credential

Create an app registration and federated credential for GitHub Actions ([Microsoft docs](https://learn.microsoft.com/en-us/azure/developer/github/connect-from-azure-openid-connect)). Use subject `repo:<owner>/<repo>:environment:prod`. Grant **Contributor** on resource group `tangle-study-prod`.

### 2. GitHub Environment `prod`

**Variables:**


| Variable                | Example                                                              |
| ----------------------- | -------------------------------------------------------------------- |
| `AZURE_CLIENT_ID`       | App registration client ID                                           |
| `AZURE_TENANT_ID`       | Azure AD tenant ID                                                   |
| `AZURE_SUBSCRIPTION_ID` | Subscription GUID                                                    |
| `AZURE_RESOURCE_GROUP`  | `tangle-study-prod`                                                  |
| `CONTAINER_REGISTRY`    | `ghcr.io/your-org/tangle-study` (optional; defaults from repo owner) |


**Secrets:**


| Secret                                  | Required | Notes                                                                                              |
| --------------------------------------- | -------- | -------------------------------------------------------------------------------------------------- |
| `BLOB_CONNECTION_STRING`                | Yes      | From Azure Storage account (created by Bicep)                                                      |
| `JWT_SECRET`                            | Yes      | Min 32 chars                                                                                       |
| `WORKER_CALLBACK_SECRET`                | Yes      | Shared with media worker                                                                           |
| `METRICS_SCRAPE_SECRET`                 | Yes      | Random string; API, Prometheus, and workers                                                        |
| `POSTGRES_CONNECTION_STRING`            | Yes      | Neon connection string — Npgsql (`Host=...;Database=...;Username=...;Password=...;SSL Mode=Require |
| `GRAFANA_ADMIN_PASSWORD`                | Yes      | Grafana admin login on external ACA app                                                            |
| `PLACES_API_KEY`                        | No       | Google Places / Geocoding                                                                          |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | No       | Auto-fetched from Azure App Insights when unset                                                    |
| `GHCR_REGISTRY_USERNAME`                | No       | Only for **private** GHCR packages                                                                 |
| `GHCR_REGISTRY_PASSWORD`                | No       | GitHub PAT with `read:packages`                                                                    |


Use Neon **direct** (non-pooler) hostname for API and migrate at study scale. Either format works (Npgsql accepts both):

`Host=ep-....neon.tech;Database=neondb;Username=...;Password=...;SSL Mode=Require;Pooling=true` (or `VerifyCA` / `VerifyFull`)

`postgresql://user:password@ep-....neon.tech/neondb?sslmode=require` (or `verify-ca` / `verify-full`)

### 3. Provision infra (once)

```bash
./scripts/azure-deploy-infra.sh prod
```

Create a Neon project + database and set `POSTGRES_CONNECTION_STRING` in GitHub. Copy the storage account connection string into GitHub secret `BLOB_CONNECTION_STRING`.

### 4. CD pipeline

After CI passes on `main`, [cd-v1.yml](../.github/workflows/cd-v1.yml):

1. Compiles once (`dotnet-publish.sh`, `build-workers-release.sh`), then builds and pushes runtime images to GHCR: `tangle-study-api`, `tangle-study-web`, `tangle-study-worker-media`, `tangle-study-worker-chat`, `tangle-study-worker-location`, plus monitoring images (`[azure-cd-build-push.sh](../scripts/cd/azure-cd-build-push.sh)`)
2. Waits for GHCR image propagation (`[azure-cd-wait-image.sh](../scripts/cd/azure-cd-wait-image.sh)`)
3. Injects secrets into Container Apps (Neon, monitoring, JWT, blob, etc.) (`[azure-cd-inject-secrets.sh](../scripts/cd/azure-cd-inject-secrets.sh)`)
4. Updates app images and env from `[parameters.prod.json](../infra/azure/parameters.prod.json)` (`[azure-cd-deploy-image.sh](../scripts/cd/azure-cd-deploy-image.sh)`) — includes `TANGLE_API_UPSTREAM` for web
5. Runs `tangle-study-migrate` job (`[azure-cd-migrate.sh](../scripts/cd/azure-cd-migrate.sh)`)
6. Smoke tests (`/health` proxied to API + SPA shell) (`[azure-cd-smoke.sh](../scripts/cd/azure-cd-smoke.sh)`)

Manual deploy: **Actions → CD-v1 → Run workflow** (uses `prod` environment).

Optional: add **required reviewers** on the `prod` environment in GitHub for approval gates.

---



## Configuration sources


| Layer                         | Purpose                                                                           |
| ----------------------------- | --------------------------------------------------------------------------------- |
| `appsettings.json`            | Base defaults                                                                     |
| `appsettings.Production.json` | Production flags (Redis on, media on, metrics auth on); empty connection strings  |
| `security.yml`                | JWT issuer/audience/expiry; placeholder secret allowed only in Development/Docker |
| `media-config.yml`            | Upload limits, `Redis:WorkQueueStreamPrefix`                                      |
| `chat-config.yml`             | Chat policy, `Redis:WorkQueueStreamPrefix` (chat-service)                         |
| **Environment variables**     | Secrets and connection strings from GitHub Actions → Container Apps               |


ASP.NET Core binds nested config with double underscores, e.g. `Redis__ConnectionString` → `Redis:ConnectionString`.

**Important:** Environment variables are re-applied after YAML in `[Program.cs](../services/Api/Program.cs)` (and `[services/Media/Program.cs](../services/Media/Program.cs)`) so GitHub-injected secrets override `security.yml` placeholders.

Each extracted service ships its own `[security.yml](../services/Media/security.yml)` with the same `Jwt` shape; production CD must inject `Jwt__Secret` on every deployable that validates bearer tokens (same value as the monolith).

---



## GitHub Environment secrets

Store these on GitHub Environment `production`. The deploy workflow maps each secret to a Container App secret ref, then to the env vars below.

### API (`services/Api`)


| GitHub secret                              | Container App env var                                                         | Required | Notes                                                                                                                 |
| ------------------------------------------ | ----------------------------------------------------------------------------- | -------- | --------------------------------------------------------------------------------------------------------------------- |
| `BLOB_CONNECTION_STRING`                   | `Media__ConnectionString`                                                     | Yes      | Azure Storage account connection string                                                                               |
| `JWT_SECRET`                               | `Jwt__Secret`                                                                 | Yes      | Min 32 chars; overrides `security.yml` placeholder                                                                    |
| `JWT_EXPIRY_MINUTES` (GitHub **variable**) | `Jwt__ExpiryMinutes`                                                          | No       | Token lifetime in minutes; default `15` from `[parameters.prod.json](../infra/azure/parameters.prod.json)` when unset |
| `WORKER_CALLBACK_SECRET`                   | `Media__WorkerCallbackSecret`                                                 | Yes      | Shared with media worker for internal callbacks                                                                       |
| `INTERNAL_SERVICE_SECRET` (planned)        | `InternalAccess__Secret` (Api), `MediaClient__InternalSecret` (Api)           | When media on ACA | Monolith ↔ media `X-Internal-Secret`; already set in Compose dev |
| `METRICS_SCRAPE_SECRET`                    | `Metrics__ScrapeSecret` (API), `METRICS_SCRAPE_SECRET` (Prometheus + workers) | Yes      | Required when `Metrics:RequireScrapeSecret` is true                                                                   |
| `POSTGRES_CONNECTION_STRING`               | `ConnectionStrings__DefaultConnection` (API + migrate)                        | Yes      | Neon Npgsql connection string from console                                                                            |
| `GRAFANA_ADMIN_PASSWORD`                   | `GF_SECURITY_ADMIN_PASSWORD` (Grafana)                                        | Yes      | External Grafana Container App admin password                                                                         |
| `PLACES_API_KEY`                           | `Places__ApiKey`                                                              | No       | Google Places / Geocoding; leave empty to disable search                                                              |

### Media (`services/Media`)

**Compose:** deployed by default (see [Local Compose (media extracted)](#local-compose-media-extracted)).

**Azure:** not in Bicep/CD yet. When added, map secrets and wire strangler routing:

| GitHub secret                | Container App env var              | Required | Notes                                                                 |
| ---------------------------- | ---------------------------------- | -------- | --------------------------------------------------------------------- |
| `JWT_SECRET`                 | `Jwt__Secret`                      | Yes      | **Same value as monolith** — validates tokens issued by Api login     |
| `BLOB_CONNECTION_STRING`     | `Media__ConnectionString`          | Yes      | Azure Storage account connection string                               |
| `WORKER_CALLBACK_SECRET`     | `Media__WorkerCallbackSecret`      | Yes      | Shared with `worker-media` for `PATCH /internal/media/.../processed` |
| `POSTGRES_CONNECTION_STRING` | `ConnectionStrings__DefaultConnection` | Yes  | Shared Neon; media uses `media` schema — run Media EF migrate job   |
| `METRICS_SCRAPE_SECRET`      | `Metrics__ScrapeSecret`            | Yes      | When `Metrics:RequireScrapeSecret` is true                            |
| `INTERNAL_SERVICE_SECRET` (new) | `Media__InternalServiceSecret` | Yes   | Monolith → media `X-Internal-Secret`; same value on Api `InternalAccess__Secret` / `MediaClient__InternalSecret` |

Non-secrets at deploy time:

| Variable / Bicep output      | Container App env var       | Example                                       |
| ---------------------------- | --------------------------- | --------------------------------------------- |
| —                            | `Monolith__BaseUrl`         | `http://tangle-study-api` (ACA short name)    |
| `MEDIA_PUBLIC_BLOB_ENDPOINT` | `Media__PublicBlobEndpoint` | `https://tanglestaging.blob.core.windows.net` |
| —                            | `Redis__ConnectionString`   | `tangle-study-redis` (ACA short name)         |

Queue stream prefix comes from each service's `*-config.yml` (`Redis:WorkQueueStreamPrefix`); override with `Redis__WorkQueueStreamPrefix` if needed.

**Web nginx (Azure cutover):** add `TANGLE_MEDIA_UPSTREAM` (or equivalent) to [`nginx.production.conf`](../infra/nginx/nginx.production.conf) mirroring local [`nginx.conf`](../infra/nginx/nginx.conf) media `location` blocks. Until then, production keeps serving `/api/media/*` from the monolith.

**Worker:** change `API_BASE_URL` on `tangle-study-worker-media` from `http://tangle-study-api` to `http://tangle-study-media` when the media Container App exists.

Non-secrets (`Jwt:Issuer`, `Jwt:Audience`, limits, queue stream prefix) stay in each service's `security.yml` / `*-config.yml` baked into the image.

**Shared (all Container Apps):**

| GitHub secret / variable                   | Container App env var                                                         | Required | Notes                                                                                                                 |
| ------------------------------------------ | ----------------------------------------------------------------------------- | -------- | --------------------------------------------------------------------------------------------------------------------- |
| `APPLICATIONINSIGHTS_CONNECTION_STRING`    | `APPLICATIONINSIGHTS_CONNECTION_STRING`                                       | No       | Auto-resolved from `tanglestudyprod-appi` when unset                                                                  |
| `GHCR_REGISTRY_USERNAME`                   | (registry pull on all apps + migrate job)                                     | No       | Only if GHCR packages are **private**                                                                                 |
| `GHCR_REGISTRY_PASSWORD`                   | (registry pull)                                                               | No       | GitHub PAT with `read:packages`                                                                                       |


**Not GitHub secrets:** Redis host is set at CD deploy (`tangle-study-redis` on API; `redis://tangle-study-redis` on workers — ACA short names, no port). Postgres-exporter DSN is derived from `POSTGRES_CONNECTION_STRING` at CD time. Blob public endpoint and media container name are non-secret Bicep outputs.

Non-secret config can be GitHub **variables** or Bicep parameters:


| Variable                     | Container App env var       | Example                                       |
| ---------------------------- | --------------------------- | --------------------------------------------- |
| `JWT_EXPIRY_MINUTES`         | `Jwt__ExpiryMinutes` (API)  | `15`                                          |
| `MEDIA_PUBLIC_BLOB_ENDPOINT` | `Media__PublicBlobEndpoint` | `https://tanglestaging.blob.core.windows.net` |
| `REDIS_INSTANCE_NAME`        | `Redis__InstanceName`       | `tangle:` (default)                           |




### Web (`clients/web` nginx)


| Variable | Env var               | Default            | Notes                                                          |
| -------- | --------------------- | ------------------ | -------------------------------------------------------------- |
| —        | `TANGLE_API_UPSTREAM` | `tangle-study-api` | Internal API short name (ACA HTTP ingress port 80; no `:8080`) |


Build the web image with `--build-arg NGINX_CONF=nginx.production.conf` for Azure (no Azurite proxy). See `[infra/nginx/nginx.production.conf](../infra/nginx/nginx.production.conf)`.

### Rust workers ([`workers/`](../workers/README.md))


| GitHub secret            | Env var                           | Required                 | Notes                                                    |
| ------------------------ | --------------------------------- | ------------------------ | -------------------------------------------------------- |
| `BLOB_CONNECTION_STRING` | `AZURE_STORAGE_CONNECTION_STRING` | Media worker only        | Same storage account as API                              |
| `WORKER_CALLBACK_SECRET` | `WORKER_CALLBACK_SECRET`          | Media + location workers | Must match `Media__WorkerCallbackSecret` on **media-service** (Compose/Azure target) or Api until Azure cutover |
| `METRICS_SCRAPE_SECRET`  | `METRICS_SCRAPE_SECRET`           | Yes                      | Protects `/metrics`; Prometheus sends `X-Metrics-Secret` |


Redis URL and API base URL are set at CD deploy time. **Today (Azure):** `API_BASE_URL=http://tangle-study-api` for media and location workers (monolith still owns those callbacks on Azure). **After ACA cutover:** media worker uses `http://tangle-study-media`; location worker uses `http://tangle-study-location`; chat worker uses `http://tangle-study-chat`.

Per-worker settings in `[parameters.prod.json](../infra/azure/parameters.prod.json)`:


| Variable               | Env var                | Example                                                      |
| ---------------------- | ---------------------- | ------------------------------------------------------------ |
| `API_BASE_URL`         | `API_BASE_URL`         | Compose: `http://media:8080` (media worker). Azure today: `http://tangle-study-api`; target: `http://tangle-study-media` |
| `WORKER_STREAM_KEY`    | `WORKER_STREAM_KEY`    | `media.uploaded`, `chat.message.created`, `location.cluster` |
| `MEDIA_CONTAINER_NAME` | `MEDIA_CONTAINER_NAME` | `tangle-media`                                               |


---



## Database migrations

Production and staging **do not** apply migrations on API startup. Migrations run as a separate Container Apps Job (`tangle-study-migrate`) that executes `dotnet Api.dll --migrate` from the API image.

### Command

```bash
dotnet Api.dll --migrate
```

Implemented in `[DatabaseMigrationRunner.cs](../services/Api/Global/Db/DatabaseMigrationRunner.cs)`. Requires `ConnectionStrings__DefaultConnection` (or `appsettings.Production.json` + env override).

### Local (Compose Postgres)

```bash
./scripts/migrate.sh
```

Uses the `api` image against the compose `db` service (`ASPNETCORE_ENVIRONMENT=Docker`).

### Staging / production (manual)

Run the migrate job against Neon before relying on a new API build:

```bash
ConnectionStrings__DefaultConnection="$POSTGRES_CONNECTION_STRING" \
  ./scripts/migrate.sh --production
```



### CD step (Container Apps Job)

The deploy workflow runs migrations automatically **after** images are deployed (the job needs the new API image on GHCR and the migrate job updated first):

1. Build and push images to GHCR (API image tag matches app deploy).
2. Inject secrets and deploy all Container App images from `[parameters.prod.json](../infra/azure/parameters.prod.json)` — including the API revision and the migrate job image (`[azure-cd-deploy-image.sh](../scripts/cd/azure-cd-deploy-image.sh)`).
3. Start `tangle-study-migrate` (`[azure-cd-migrate.sh](../scripts/cd/azure-cd-migrate.sh)`); proceed only if execution status is `Succeeded`.
4. Smoke test via web ingress (`[azure-cd-smoke.sh](../scripts/cd/azure-cd-smoke.sh)`).

Skip on manual runs: **Deploy → Run workflow → Skip EF migrate job**.

Development/Docker still auto-migrate on API startup for local convenience.

---



## Observability



### Application Insights

Bicep provisions workspace-based **Application Insights** (free tier eligible) and sets `APPLICATIONINSIGHTS_CONNECTION_STRING` on `tangle-study-api`. The API enables [Azure Monitor OpenTelemetry](https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-enable?tabs=aspnetcore) when that variable is present — see `[AzureMonitorTelemetryExtensions.cs](../services/Api/Global/Telemetry/AzureMonitorTelemetryExtensions.cs)`.

Container Apps platform logs go to the same Log Analytics workspace.

### Prometheus + Grafana on ACA

Bicep deploys a **monitoring stack** on Container Apps (Compose parity). **CD** builds custom
GHCR images (`tangle-study-prometheus`, `tangle-study-grafana`) that bundle provisioning from
`[infra/grafana/provisioning/](../infra/grafana/provisioning/)` and ACA scrape entrypoints from
`[infra/azure/monitoring/](../infra/azure/monitoring/)`. Deploy injects cross-app URLs from
`[azure-aca-urls.sh](../scripts/cd/libs/azure-aca-urls.sh)` via `[azure-cd-deploy-image.sh](../scripts/cd/azure-cd-deploy-image.sh)`
(e.g. `PROMETHEUS_URL=http://tangle-study-prometheus`, `REDIS_URL=redis://tangle-study-redis`).
HTTP short app names must omit `targetPort` ([ACA short names](../infra/azure/README.md#aca-short-names-do-not-append-targetport)).


| App                              | Access              | Notes                                                                                           |
| -------------------------------- | ------------------- | ----------------------------------------------------------------------------------------------- |
| `tangle-study-grafana`           | External HTTPS FQDN | Custom image; `admin` / `GRAFANA_ADMIN_PASSWORD`; datasource → `http://tangle-study-prometheus` |
| `tangle-study-prometheus`        | Internal only       | Custom image; scrapes API, workers, exporters (short names)                                     |
| `tangle-study-postgres-exporter` | Internal            | Connects to Neon                                                                                |
| `tangle-study-redis-exporter`    | Internal            | Scrapes internal Redis                                                                          |


Resolve Grafana URL after infra deploy:

```bash
az containerapp show --name tangle-study-grafana --resource-group tangle-study-prod \
  --query properties.configuration.ingress.fqdn -o tsv
```

Dashboards, recording rules, and alerts match local Compose — see `[infra/README.md](../infra/README.md)`. Workers require `X-Metrics-Secret` on `/metrics` when `METRICS_SCRAPE_SECRET` is set (same as API).

Local Prometheus/Grafana under `[infra/](../infra/)` remain for Docker Compose (`--profile monitoring`).

### Post-deploy smoke tests

After migrate, [cd-v1.yml](../.github/workflows/cd-v1.yml) runs `[scripts/cd/azure-cd-smoke.sh](../scripts/cd/azure-cd-smoke.sh)`:

1. Waits for **API and web** Container App revisions to reach `healthState=Healthy`
2. `GET https://<tangle-study-web-fqdn>/` — SPA shell loads
3. `GET https://<tangle-study-web-fqdn>/health` — expects `Healthy` (proxied to API)

Manual run:

```bash
AZURE_RESOURCE_GROUP=tangle-study-prod ./scripts/cd/azure-cd-smoke.sh
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

Automated smoke tests (`[azure-cd-smoke.sh](../scripts/cd/azure-cd-smoke.sh)`) only cover `/health` and the SPA shell — they do not replace this checklist.

### Pre-flight

- [x] CD workflow succeeded (build → secrets → image update → migrate → smoke)
- [x] `GET https://<fqdn>/health` returns `Healthy`
- [x] Blob CORS allows `PUT` from the web origin (required for browser uploads)
- [x] `Media__PublicBlobEndpoint` points at the HTTPS blob endpoint clients use for SAS uploads
- [ ] Application Insights shows incoming requests after browsing the app (Azure portal → `tanglestudyprod-appi`)



### Auth & users

- [x] Register a new account (`/register`)
- [x] Log in and reach the home page (`/login`)
- [x] Session persists across refresh (JWT in local storage)
- [x] View another user's profile (`/users/:id`)
- [x] Log out; protected routes redirect to `/login`



### Posts & comments

- [x] Create a text post (`/posts/new`)
- [x] View post detail; add a comment
- [x] Edit and delete own post/comment
- [x] Upload an image on a post (media worker processes thumbnail — allow ~30s if workers scaled to zero)



### Friends & blocks

- [x] Send friend request (User A → User B); accept on B
- [ ] Block a user; verify blocked content is hidden
- [ ] Unblock



### Groups

- [x] Create a group (`/groups/new`)
- [x] Invite / accept member
- [x] Create a board; add a board post with optional media
- [x] Group chat room appears; send a message



### Chat (SignalR)

- [x] Open group chat room; message appears in realtime for another member (no manual refresh)
- [x] Send a chat message with an image attachment (media worker)



### Memory Map & location (Phase 7 gate)

Requires `PLACES_API_KEY` for place search. Workers may cold-start from zero replicas — wait up to ~1 min for cluster jobs.

- [x] Map loads at `/map` (OpenStreetMap tiles)
- [x] Place search returns results (if Places API key configured)
- [x] Double-click to drop a pin; pin visible after refresh
- [ ] Start live location sharing in a group; User B sees User A's marker
- [ ] Stop sharing; marker disappears for User B
- [ ] SOS alert received by group members while sharing (SignalR `/hubs/location`)
- [x] Zoom out to cluster view (zoom 2–4); clusters appear after location worker runs



### Workers & async paths

Confirm worker Container Apps (`tangle-study-worker-media`, `tangle-study-worker-chat`, `tangle-study-worker-location`) scale up after triggering the feature above. Check execution logs in Log Analytics if a job stalls.

- [ ] Media upload → thumbnail/variant available in UI
- [ ] Chat message → delivered without page reload
- [ ] Location cluster job completes (cluster markers at low zoom)



### Observability

- [x] API requests visible in Application Insights (Live Metrics or Transactions)
- [x] Grafana external URL loads; Prometheus targets UP (api, postgres, redis, workers)
- [x] Container Apps logs stream without repeated crash loops (`az containerapp logs show -n tangle-study-api -g tangle-study-prod --tail 50`)
- [x] No sustained 5xx on `/api/*` during the walkthrough



### Sign-off

When all required boxes are checked, record the validating commit SHA and date here (or in your project notes):

```
Validated: <YYYY-MM-DD> @ <git-sha> on https://<fqdn>
```

Only then proceed to [MSA extraction](MSA_MIGRATION.md#extraction-order).

---



## Troubleshooting (migrate job / Neon connection string)

**Symptom:** `tangle-study-migrate` fails with `Couldn't set postgresql://...?sslmode` or `ArgumentException` from `NpgsqlConnectionStringBuilder`.

**Cause:** `POSTGRES_CONNECTION_STRING` is truncated — commonly ending at `?sslmode` without `=require`. Npgsql cannot parse the URI.

**Fix:**

1. In **GitHub → Environments → prod → Secrets**, set a complete string (copy from Neon console; do not truncate):
  ```text
   Host=ep-....neon.tech;Database=neondb;Username=...;Password=...;SSL Mode=Require;Pooling=true
  ```
   (`SSL Mode=VerifyCA` or `VerifyFull` also accepted; `Require` is simplest for ACA containers.)
   or
   (`sslmode=verify-ca` or `verify-full` also accepted.)
2. Confirm the connection string format (Npgsql or URI) matches Neon console output before setting the GitHub secret.
3. Re-inject and re-run migrate:
  ```bash
   AZURE_RESOURCE_GROUP=tangle-study-prod \
   POSTGRES_CONNECTION_STRING='...' \
   BLOB_CONNECTION_STRING='...' \
   JWT_SECRET='...' \
   WORKER_CALLBACK_SECRET='...' \
   METRICS_SCRAPE_SECRET='...' \
   GRAFANA_ADMIN_PASSWORD='...' \
     bash scripts/cd/azure-cd-inject-secrets.sh

   AZURE_RESOURCE_GROUP=tangle-study-prod bash scripts/cd/azure-cd-migrate.sh
  ```
   Or trigger **Actions → CD-v1 → Run workflow**.

CD now rejects malformed strings in `[scripts/cd/azure-cd-inject-secrets.sh](../scripts/cd/azure-cd-inject-secrets.sh)` before injecting secrets. Migrate job logs are dumped automatically on failure in `[scripts/cd/azure-cd-migrate.sh](../scripts/cd/azure-cd-migrate.sh)`.

If the password appeared in Log Analytics, rotate it in the Neon console.

---



## Troubleshooting (Redis on ACA)

**Symptom:** API logs show `StackExchange.Redis.ConnectionMultiplexer.Connect` failure at startup; revision crash-loops (older builds) or `/health` reports Redis **Unhealthy**.

**Cause:** The Redis **container can be healthy** while **cross-app TCP** from `tangle-study-api` to `tangle-study-redis` fails. Common reasons:

- `tangle-study-redis` deployed **without internal TCP ingress**
- Ingress metadata looks correct but ACA Envoy routing is **stale** (same class as historical Postgres TCP issues)

The API connection string uses the **short app name** `tangle-study-redis` (port 6379 implicit; set by CD).

**Fix:** Verify Redis ingress and API env, then re-deploy and re-run smoke:

```bash
az containerapp show -n tangle-study-redis -g tangle-study-prod \
  --query "properties.configuration.ingress.{transport:transport,targetPort:targetPort,fqdn:fqdn}" -o yaml

az containerapp show -n tangle-study-api -g tangle-study-prod \
  --query "properties.template.containers[0].env[?name=='Redis__ConnectionString']" -o yaml

AZURE_RESOURCE_GROUP=tangle-study-prod bash scripts/cd/azure-cd-deploy-image.sh
AZURE_RESOURCE_GROUP=tangle-study-prod ./scripts/cd/azure-cd-smoke.sh
```

Manual cross-app probe from your laptop (use short name `tangle-study-redis`):

```bash
az containerapp exec -n tangle-study-api -g tangle-study-prod \
  --container tangle-study-api \
  --command "timeout 5 bash -c 'echo > /dev/tcp/tangle-study-redis/6379' && echo OK || echo FAIL"
```

From CI or a non-interactive shell, wrap exec:

```bash
script -q -c "az containerapp exec -n tangle-study-api -g tangle-study-prod \
  --container tangle-study-api \
  --command \"timeout 5 bash -c 'echo > /dev/tcp/tangle-study-redis/6379' && echo OK || echo FAIL\"" /dev/null
```

`Redis__ConnectionString` includes StackExchange.Redis options (`abortConnect=false`, connect timeouts). The API sets `AbortOnConnectFail=false` so a transient Redis blip does not crash-loop; `/health` shows **Unhealthy** until Redis is reachable.

Fresh infra deploys pick up TCP ingress from `[infra/azure/modules/infra-container.bicep](../infra/azure/modules/infra-container.bicep)` when `tcpProbePort` is set (6379 for Redis).

---



## ACA HTTP short names vs `targetPort` (504 / timeout)

Within a Container Apps Environment, callers reach HTTP ingress apps by **short app name**
(`tangle-study-api`). Unlike Docker Compose (`api:8080`), you must **not** append the
container `targetPort` to the short name on ACA.


| URL from another Container App                      | What happens                                            |
| --------------------------------------------------- | ------------------------------------------------------- |
| `http://tangle-study-api/health`                    | Ingress port 80 → forwards to container :8080 → **200** |
| `http://tangle-study-api:8080/health`               | Connects to pod IP :8080 directly → **timeout**         |
| `https://tangle-study-api.internal.<domain>/health` | Internal FQDN on :443 → **200**                         |


The short name DNS record points at the **ingress front door** (port 80), not the container
listen port. `:8080` bypasses ingress and hits an unreachable pod address — nginx then returns
**504** even though a direct curl to `http://tangle-study-api/health` succeeds.

**Web nginx:** `[TANGLE_API_UPSTREAM](../infra/azure/parameters.prod.json)` must be
`tangle-study-api` (no port). See `[infra/azure/README.md](../infra/azure/README.md#aca-short-names-do-not-append-targetport)`.

**Diagnose from the web container:**

```bash
grep -A1 'upstream tangle_api' /etc/nginx/conf.d/default.conf   # must NOT show :8080
curl -sf http://tangle-study-api/health                          # direct — should work
curl -sv http://127.0.0.1/health                                # via nginx — 504 if upstream wrong
```

**Fix:** redeploy web so nginx re-renders config at startup:

```bash
az containerapp update -n tangle-study-web -g tangle-study-prod \
  --set-env-vars TANGLE_API_UPSTREAM=tangle-study-api TANGLE_API_HOST=tangle-study-api
```

---



## Troubleshooting (smoke `/health` failures)

`[azure-cd-smoke.sh](../scripts/cd/azure-cd-smoke.sh)` hits the public **web** URL. `/health` is proxied to the internal API (`TANGLE_API_UPSTREAM`). CD waits for API/web revisions to be `Healthy` before curling.


| HTTP code           | Meaning                                     | What to check                                                                                                                                                           |
| ------------------- | ------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **000**             | curl timed out (no bytes)                   | API revision still activating, readiness failing, or nginx upstream unreachable (see [ACA short names vs targetPort](#aca-http-short-names-vs-targetport-504--timeout)) |
| **404**             | web/nginx responded; API returned not found | Wrong path (use `/health`, not `/api/health`) or stale routing                                                                                                          |
| **502 / 503 / 504** | web/nginx reached or waited on API          | **504** often means `TANGLE_API_UPSTREAM` includes `:8080`; **503** means API dependency unhealthy                                                                      |




### HTTP 000 (timeout)

**Symptom:** Smoke logs `HTTP 000` and `curl: (28) Operation timed out ... 0 bytes received`.

**Cause:** CD curled before the API revision was ready to serve traffic, or nginx hung on an unreachable upstream (e.g. `tangle-study-api:8080` — ACA short names must omit the port; use `tangle-study-api` only).

**Fix:** Re-run smoke after revisions settle:

```bash
AZURE_RESOURCE_GROUP=tangle-study-prod ./scripts/cd/azure-cd-smoke.sh
```

Check revision status:

```bash
az containerapp revision list -n tangle-study-api -g tangle-study-prod \
  --query "[?properties.active==\`true\`].{revision:name,health:properties.healthState,running:properties.runningState}" -o yaml
```

Emergency bypass (not recommended):

```bash
SMOKE_SKIP_API_WAIT=1 AZURE_RESOURCE_GROUP=tangle-study-prod ./scripts/cd/azure-cd-smoke.sh
```



### HTTP 404

**Symptom:** `[azure-cd-smoke.sh](../scripts/cd/azure-cd-smoke.sh)` fails; web nginx logs show `GET /health HTTP/1.1" 404`.

**Cause:** Web nginx proxies `/health` to the internal API (`TANGLE_API_UPSTREAM`, default `tangle-study-api`). ACA HTTP ingress listens on port 80 for short app names — do **not** append `:8080` (connects to pod IP and times out). Infra deployed with **placeholder images** may also drift `targetPort` until CD reconciles env from `[parameters.prod.json](../infra/azure/parameters.prod.json)`.

**Fix:** Re-run deploy-image to reconcile `TANGLE_API_UPSTREAM` from `[parameters.prod.json](../infra/azure/parameters.prod.json)`, then smoke:

```bash
AZURE_RESOURCE_GROUP=tangle-study-prod bash scripts/cd/azure-cd-deploy-image.sh
AZURE_RESOURCE_GROUP=tangle-study-prod ./scripts/cd/azure-cd-smoke.sh
```

Verify:

```bash
az containerapp show -n tangle-study-api -g tangle-study-prod \
  --query "properties.configuration.ingress.targetPort" -o tsv   # expect 8080

az containerapp show -n tangle-study-web -g tangle-study-prod \
  --query "properties.template.containers[0].env[?name=='TANGLE_API_UPSTREAM']" -o yaml
# expect value: tangle-study-api
```

If `/health` still fails with **503** or `Unhealthy`, the API is up but a dependency check failed (Postgres, Redis) — check `tangle-study-api` logs.

---



## Production API checklist

Before first deploy:

1. Set all required GitHub secrets for the target environment (including `POSTGRES_CONNECTION_STRING` and `GRAFANA_ADMIN_PASSWORD`).
2. Set `Media__PublicBlobEndpoint` to the blob account URL clients use for SAS uploads (HTTPS).
3. Configure blob CORS on the storage account for the web app origin (`PUT` from browser).
4. Enable Redis — required for SignalR backplane and work queues when the API scales beyond one replica.
5. Run `./scripts/migrate.sh --production` (or the Container Apps migrate job) as part of each CD deploy — startup does not migrate in Production.

JWT placeholder in `[security.yml](../services/Api/security.yml)` causes startup failure outside Development/Docker unless `Jwt__Secret` is injected.

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

- [ARCHITECTURE.md](ARCHITECTURE.md) — monolith + media-service in Compose
- [MSA_MIGRATION.md](MSA_MIGRATION.md) — Phase 9 extraction progress and Azure follow-ups

