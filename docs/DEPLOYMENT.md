# Deployment

Azure Container Apps deployment for the Tangle monolith. Secrets are injected from **GitHub Environment secrets** at deploy time via the CD workflow (`deploy.yml`, planned).

Local development continues to use Docker Compose — see [README](../README.md).

---

## Environments

| Environment | GitHub Environment | Trigger (planned) |
|-------------|-------------------|-------------------|
| dev | `dev` | Push to `develop` |
| staging | `staging` | Push to `main` |
| prod | `prod` | Release / manual approval |

Set `ASPNETCORE_ENVIRONMENT=Production` on the API Container App so [`appsettings.Production.json`](../services/Api/appsettings.Production.json) loads.

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

Store these per GitHub Environment (`dev`, `staging`, `prod`). The deploy workflow maps each secret to a Container App secret ref, then to the env vars below.

### API (`services/Api`)

| GitHub secret | Container App env var | Required | Notes |
|---------------|----------------------|----------|-------|
| `POSTGRES_CONNECTION_STRING` | `ConnectionStrings__DefaultConnection` | Yes | Npgsql connection string for Azure Database for PostgreSQL |
| `REDIS_CONNECTION_STRING` | `Redis__ConnectionString` | Yes | Azure Cache for Redis hostname:port (TLS if enabled) |
| `BLOB_CONNECTION_STRING` | `Media__ConnectionString` | Yes | Azure Storage account connection string |
| `JWT_SECRET` | `Jwt__Secret` | Yes | Min 32 chars; overrides `security.yml` placeholder |
| `WORKER_CALLBACK_SECRET` | `Media__WorkerCallbackSecret` | Yes | Shared with media worker for internal callbacks |
| `METRICS_SCRAPE_SECRET` | `Metrics__ScrapeSecret` | Yes | Required when `Metrics:RequireScrapeSecret` is true |
| `PLACES_API_KEY` | `Places__ApiKey` | No | Google Places / Geocoding; leave empty to disable search |

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
| `REDIS_CONNECTION_STRING` | `REDIS_URL` | Yes | Prefix with `redis://` or `rediss://` as required by Azure Redis |
| `BLOB_CONNECTION_STRING` | `AZURE_STORAGE_CONNECTION_STRING` | Media worker only | Same storage account as API |
| `WORKER_CALLBACK_SECRET` | `WORKER_CALLBACK_SECRET` | Media worker only | Must match `Media__WorkerCallbackSecret` |

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

### Planned CD step (Container Apps Job)

The deploy workflow will run the same command using the API image:

1. Build and push API image to ACR.
2. Start a **Container Apps Job** (or one-shot `az containerapp exec`) with `dotnet Api.dll --migrate` and `POSTGRES_CONNECTION_STRING`.
3. Roll out the new API revision only if migrations succeed.

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
