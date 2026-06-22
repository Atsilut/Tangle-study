# Deployment

Azure Container Apps deployment for the Tangle monolith. Secrets are injected from **GitHub Environment secrets** at deploy time via [`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml).

Local development continues to use Docker Compose — see [README](../README.md).

---

## Environments

| Environment | GitHub Environment | Trigger |
|-------------|-------------------|---------|
| production | `production` | CI success on `main`, or manual `workflow_dispatch` |

`develop` runs CI only — no automatic deploy. Local experiments may use `tangle-dev` via `./scripts/azure-deploy-infra.sh dev`.

Set `ASPNETCORE_ENVIRONMENT=Production` on the API Container App so [`appsettings.Production.json`](../services/Api/appsettings.Production.json) loads.

---

## Container image versions

Build and deploy use **pinned** base images from [`docker/versions.prod.env`](../docker/versions.prod.env). App images are published to **GHCR** (not ACR). Pass the same build-args when building in CI:

| Build-arg | Prod value (example) |
|-----------|---------------------|
| `DOTNET_SDK_IMAGE` / `DOTNET_ASPNET_IMAGE` | `mcr.microsoft.com/dotnet/sdk:10.0` |
| `NODE_IMAGE` / `NGINX_IMAGE` | `node:26.3-bookworm-slim`, `nginx:1.31.1-alpine` |
| `RUST_IMAGE` / `DEBIAN_IMAGE` | `rust:1.96.0-bookworm`, `debian:bookworm-slim` |

On Azure, **Postgres and Redis run as Container Apps** (same pattern as local Compose), not managed Azure Database / Azure Cache. Blob storage uses a standard Storage account. See [`infra/azure/README.md`](../infra/azure/README.md).

See [`docker/README.md`](../docker/README.md).

---

## Azure infrastructure (Bicep)

Study-friendly stack — **no managed PostgreSQL, Redis, or ACR** (those lack a useful free tier). Templates: [`infra/azure/`](../infra/azure/).

| Compose (local) | Azure |
|-----------------|-------|
| `db` | `tangle-postgres` Container App |
| `redis` | `tangle-redis` Container App |
| `azurite` | Storage account (blob) |
| Built images | **GHCR** (`ghcr.io/<org>/tangle-study/...`) |

```bash
POSTGRES_ADMIN_PASSWORD='...' ./scripts/azure-deploy-infra.sh prod
```

Bicep sets the Postgres connection string and Redis URL from internal app hostnames. **App secrets** (JWT, blob, etc.) are injected after deploy via GitHub Actions. Details: [`infra/azure/README.md`](../infra/azure/README.md).

---

## GitHub setup (one-time)

### 1. Azure OIDC federated credential

Create an app registration and federated credential for GitHub Actions ([Microsoft docs](https://learn.microsoft.com/en-us/azure/developer/github/connect-from-azure-openid-connect)). Use subject `repo:<owner>/<repo>:environment:production`. Grant **Contributor** on resource group `tangle-prod`.

### 2. GitHub Environment `production`

**Variables:**

| Variable | Example |
|----------|---------|
| `AZURE_CLIENT_ID` | App registration client ID |
| `AZURE_TENANT_ID` | Azure AD tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Subscription GUID |
| `AZURE_RESOURCE_GROUP` | `tangle-prod` |
| `CONTAINER_REGISTRY` | `ghcr.io/your-org/tangle-study` (optional; defaults from repo owner) |

**Secrets:**

| Secret | Notes |
|--------|-------|
| `BLOB_CONNECTION_STRING` | From Azure Storage account (created by Bicep) |
| `JWT_SECRET` | Min 32 chars |
| `WORKER_CALLBACK_SECRET` | Shared with media worker |
| `METRICS_SCRAPE_SECRET` | Random string |
| `PLACES_API_KEY` | Optional |

Postgres password: set at **infra deploy** (`POSTGRES_ADMIN_PASSWORD`), not a GitHub secret.

### 3. Provision infra (once)

```bash
POSTGRES_ADMIN_PASSWORD='...' ./scripts/azure-deploy-infra.sh prod
```

Copy the storage account connection string into GitHub secret `BLOB_CONNECTION_STRING` on the `production` environment.

### 4. CD pipeline

After CI passes on **`main`**, [deploy.yml](../.github/workflows/deploy.yml):

1. Builds and pushes `tangle-api`, `tangle-web`, `tangle-worker` to GHCR
2. Injects secrets into Container Apps
3. Updates app images to the commit SHA
4. Runs `tangle-migrate` job

Manual deploy: **Actions → Deploy → Run workflow** (uses `production` environment).

Optional: add **required reviewers** on the `production` environment in GitHub for approval gates.

---

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
| `METRICS_SCRAPE_SECRET` | `Metrics__ScrapeSecret` | Yes | Required when `Metrics:RequireScrapeSecret` is true |
| `PLACES_API_KEY` | `Places__ApiKey` | No | Google Places / Geocoding; leave empty to disable search |
| `GHCR_REGISTRY_PASSWORD` | (deploy workflow) | No | Only if GHCR packages are **private** |

**Not GitHub secrets:** Postgres password is passed at infra deploy (`POSTGRES_ADMIN_PASSWORD`). Redis host is set by Bicep (`tangle-redis:6379`). Do not use managed Azure Postgres/Redis/ACR in this study setup.

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

Redis URL is set by Bicep (`REDIS_URL=redis://tangle-redis:6379`).

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
2. Update migrate job image, then `az containerapp job start` on `tangle-migrate`.
3. Proceed only if the job execution status is `Succeeded`.

Skip on manual runs: **Deploy → Run workflow → Skip EF migrate job**.

Development/Docker still auto-migrate on API startup for local convenience.

---

## Production API checklist

Before first deploy:

1. Set all required GitHub secrets for the target environment.
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
  -t tangle-web .
```

---

## Related docs

- [ARCHITECTURE.md](ARCHITECTURE.md) — current monolith layout
- [MSA_MIGRATION.md](MSA_MIGRATION.md) — Phase 9 service extraction (after prod monolith is proven)
