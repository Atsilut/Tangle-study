# Monitoring infrastructure

Prometheus + Grafana stack (Phase 5) with provisioned alerts, recording rules, and infra exporters. Does not run by default.

Distributed tracing and log aggregation are not part of this profile — planned later with Grafana Alloy + Loki + Tempo.

## Layout

```
infra/
  prometheus/
    prometheus.yml                   # core scrape targets + rule_files
    scrape/workers.yml               # worker jobs (loaded only with `workers` profile)
    docker-entrypoint.sh             # enables worker scrape when rust-worker exists
    recording_rules.yml              # precomputed tangle:services_* metrics
  grafana/provisioning/
    datasources/prometheus.yml       # Grafana → Prometheus datasource
    dashboards/
      dashboard.yml                  # file provider config
      tangle-overview.json           # provisioned dashboard
    alerting/
      rules-api-http.yml             # HTTP, health, scrape alerts
      rules-workers.yml              # worker job + callback alerts
      rules-infra.yml                # Postgres + Redis exporter alerts
```

## Start

Monitoring uses the Compose `monitoring` profile. The default stack (gateway + domain services) must be running; workers need the `workers` profile.

```bash
# Default stack + Prometheus + Grafana + infra exporters
docker compose --profile monitoring up --build

# Include Rust workers (chat, media, location scrape targets)
docker compose --profile monitoring --profile workers up --build
```

## Troubleshooting empty dashboards

Grafana and Prometheus are **not** started by `docker compose up` alone. If panels are empty or Prometheus has no data:

1. Start the stack with the monitoring profile: `docker compose --profile monitoring up --build`
2. Open http://localhost:9090/targets — all eight API jobs (`gateway`, `users`, `media`, `chat`, `location`, `community`, `group`, `social`) should be **UP**
3. For worker metrics, add `--profile workers` and restart Prometheus if workers were enabled after it started
4. Check the **Scrape target health (up)** panel on the Tangle Study Overview dashboard

## URLs (host)

| Service | URL | Notes |
|---------|-----|-------|
| Grafana | http://localhost:3000 | Login: `admin` / `admin` (dev default) |
| Prometheus | http://localhost:9090 | Targets page shows scrape health |
| Edge health | http://localhost:8080/health | Nginx aggregate health (plain-text `Healthy` / `Unhealthy`) |
| Service metrics | in-container `:8080/metrics` | Scraped by Prometheus on the Compose network (requires `X-Metrics-Secret` in Docker) |
| Worker metrics | in-container `:9090/metrics` | Scraped by Prometheus on the Compose network |

## Scrape targets

Core jobs in [prometheus/prometheus.yml](prometheus/prometheus.yml). Worker jobs in [prometheus/scrape/workers.yml](prometheus/scrape/workers.yml) are loaded at Prometheus startup only when the Compose `workers` profile is active (see [docker-entrypoint.sh](prometheus/docker-entrypoint.sh)).

| Job | Target | Metrics |
|-----|--------|---------|
| `gateway` | `gateway:8080` | Edge HTTP request rate, latency, status codes |
| `users` | `users:8080` | HTTP request rate, latency, status codes; health gauges |
| `media` | `media:8080` | Same |
| `chat` | `chat:8080` | Same |
| `location` | `location:8080` | Same |
| `community` | `community:8080` | Same |
| `group` | `group:8080` | Same |
| `social` | `social:8080` | Same |
| `rust-worker-chat` | `rust-worker-chat:9090` | Job processing, pending queue, DLQ length, callback responses (`workers` profile) |
| `rust-worker-media` | `rust-worker-media:9090` | Same (`workers` profile) |
| `rust-worker-location` | `rust-worker-location:9090` | Same (`workers` profile) |
| `postgres` | `postgres-exporter:9187` | Connection counts, settings, activity |
| `redis` | `redis-exporter:9121` | Memory, clients, uptime |

Without `--profile workers`, worker jobs are **not scraped** (no targets, no false `WorkerScrapeTargetDown` alert). With `--profile workers`, worker targets that are running should be **UP** when healthy.

Optional host debug ports (not mapped by default): exec into a worker container and `wget -qO- http://127.0.0.1:9090/metrics`.

## Service `/health`

Each .NET service exposes `GET /health` — a plain-text aggregate result (`Healthy` or `Unhealthy`) from ASP.NET Core health checks for PostgreSQL and Redis (when enabled). Compose healthchecks probe this endpoint for the status code only.

Per-dependency detail is not in the HTTP body. It is exported to `/metrics` as `aspnetcore_healthcheck_status{name="postgres|redis"}` (1 = healthy, 0 = unhealthy) via `prometheus-net.AspNetCore.HealthChecks`.

## Scrape-protected `/metrics`

In Docker (`appsettings.Docker.json`), `Metrics:RequireScrapeSecret` is `true`. Scrapers must send:

```http
X-Metrics-Secret: dev-metrics-secret
```

Prometheus is configured with this header in [prometheus/prometheus.yml](prometheus/prometheus.yml). Local development (`appsettings.json`) keeps auth disabled.

## Recording rules

Precomputed in [prometheus/recording_rules.yml](prometheus/recording_rules.yml) (evaluated every 15s):

| Metric | Purpose |
|--------|---------|
| `tangle:services_request_rate` | Traffic guard for ratio/latency alerts |
| `tangle:services_5xx_ratio` | 5xx share of total requests |
| `tangle:services_other_4xx_ratio` | 4xx excluding 401/403/409 |
| `tangle:services_5xx_rate_by_controller` | Per-controller 5xx rate |
| `tangle:services_request_duration_p95` | API latency SLO input |

## Dashboard

**Tangle Study Overview** (`uid: tangle-overview`) is auto-provisioned under the **Tangle Study** folder in Grafana:

- API request rate and latency (p50 / p95 with 2s threshold line)
- API 4xx error rate by status code
- API 5xx and other-4xx ratios (recording rules)
- API errors by controller; top 5xx controllers
- Worker jobs processed by outcome
- Worker callback responses by code
- Worker pending messages and DLQ length
- Work queue enqueue rate from Media, Chat, and Location
- Postgres connections and Redis memory/clients
- Scrape target health (`up`) per API job

Panels use metrics from `prometheus-net` (API) and custom `tangle_*` counters/gauges (workers).

## Alerting

Provisioned under `grafana/provisioning/alerting/`. All rules use `for: 5m` before firing. View in Grafana → **Alerting** → **Alert rules** (folder: **Tangle Study**).

No Alertmanager, Slack, or email — alerts appear in the Grafana UI only.

### API HTTP (`rules-api-http.yml`)

| Rule | Condition | Severity |
|------|-----------|----------|
| `ApiScrapeTargetDown` | any `up{job=~"users|media|chat|location|community|group|social|gateway"} == 0` | critical |
| `ApiDependencyUnhealthy` | `min(aspnetcore_healthcheck_status{job=~"users|media|chat|location|community|group|social"}) == 0` | critical |
| `ApiHigh5xxRate` | 5xx rate > **0.1 req/s** | critical |
| `ApiHigh5xxRatio` | `tangle:services_5xx_ratio > 1%` and traffic > 0.05 req/s | critical |
| `ApiHigh401Rate` | 401 rate > **0.5 req/s** | warning |
| `ApiHigh403Rate` | 403 rate > **0.1 req/s** | warning |
| `ApiHigh409Rate` | 409 rate > **0.5 req/s** | warning |
| `ApiHighOther4xxRatio` | `tangle:services_other_4xx_ratio > 10%` and traffic > 0.05 req/s | warning |
| `ApiHighLatencyP95` | `tangle:services_request_duration_p95 > 2s` and traffic > 0.05 req/s | warning |
| `ApiControllerHigh5xxRate` | any controller 5xx rate > **0.05 req/s** and traffic > 0.05 req/s | critical |

### Workers (`rules-workers.yml`)

| Rule | Condition | Severity |
|------|-----------|----------|
| `WorkerScrapeTargetDown` | `up{job=~"rust-worker.*"} == 0` (`noDataState: OK`) | warning |
| `WorkerDlqNotEmpty` | `max(tangle_worker_dlq_length) > 0` | critical |
| `WorkerHighDlqRate` | DLQ outcome rate > 0 (`outcome="dlq"`; not retryable `failure`) | warning |
| `WorkerBacklogGrowing` | `max(tangle_worker_pending_messages) > 50` | warning |
| `WorkerCallbackHigh5xxRate` | callback 5xx rate > 0 | critical |

### Infrastructure (`rules-infra.yml`)

| Rule | Condition | Severity |
|------|-----------|----------|
| `PostgresExporterDown` | `up{job="postgres"} == 0` | critical |
| `PostgresHighConnections` | connections > **80%** of `max_connections` | warning |
| `RedisExporterDown` | `up{job="redis"} == 0` | critical |
| `RedisHighMemory` | used memory > **90%** of `maxmemory` (when maxmemory is set) | warning |

### Triage quick reference

1. **Scrape DOWN** — check Compose profiles (`monitoring`, `workers`) and container logs. Worker scrape jobs exist only with `--profile workers`; restart Prometheus after enabling workers if targets are missing.
2. **Dependency unhealthy** — `docker compose logs users db redis`; verify service `/health`.
3. **5xx spike** — Grafana dashboard → errors by controller; check service logs.
4. **Worker DLQ** — `cargo run -- replay` or inspect `{stream}.dlq` in Redis.
5. **Callback 5xx** — media worker cannot reach media-service; check `API_BASE_URL` and `WORKER_CALLBACK_SECRET`.

## Verification

```bash
# Full stack
docker compose --profile monitoring --profile workers up --build

# Prometheus targets (gateway, users, media, chat, location, community, group, social, postgres, redis, workers) should be UP
open http://localhost:9090/targets

# Count healthy API scrape targets (expect 8)
curl -s 'http://localhost:9090/api/v1/query?query=count(up{job=~"gateway|users|media|chat|location|community|group|social"}==1)'

# Edge health (plain text)
curl -s http://localhost:8080/health

# Per-dependency health gauges (Prometheus — users example)
curl -s -H "X-Metrics-Secret: dev-metrics-secret" http://localhost:9090/api/v1/query?query=aspnetcore_healthcheck_status{job=\"users\"}

# Metrics endpoint auth (Docker — exec into users container)
docker compose exec users wget -qO- --header='X-Metrics-Secret: dev-metrics-secret' http://127.0.0.1:8080/metrics | head

# Integration tests (Testcontainers — use `test` profile, not `docker-dotnet.sh` / `sdk`)
./scripts/ci/docker-test.sh test services/Gateway.Tests/Gateway.Tests.csproj -c Release --filter "FullyQualifiedName~MetricsIntegrationTests|FullyQualifiedName~MetricsScrapeAuthIntegrationTests"
./scripts/ci/docker-test.sh test services/Users.Tests/Users.Tests.csproj -c Release --filter "FullyQualifiedName~MetricsIntegrationTests|FullyQualifiedName~MetricsScrapeAuthIntegrationTests"
```

## Azure Container Apps (production)

The same Prometheus/Grafana provisioning (`infra/grafana/provisioning/`, `infra/prometheus/recording_rules.yml`) is bundled into custom GHCR images for ACA:

```
infra/azure/monitoring/
  prometheus/Dockerfile          # ACA scrape config at container start
  prometheus/prometheus-start.sh # ACA scrape targets at container start (API short name)
  grafana/Dockerfile             # sed Prometheus datasource to internal URL
  grafana/grafana-start.sh
```

| Compose (local) | Azure ACA |
|-----------------|-----------|
| `prometheus:9090` (host) | `tangle-study-prometheus` (internal) |
| `grafana:3000` (host) | `tangle-study-grafana` (external FQDN) |
| `postgres-exporter` → compose `db` | `tangle-study-postgres-exporter` → **Neon** |
| `redis-exporter` → compose `redis` | `tangle-study-redis-exporter` → internal Redis |
| `api:8080/metrics` | `tangle-study-api/metrics` (short name; ACA ingress port 80) |
| `rust-worker-*:9090/metrics` | `tangle-study-worker-*` (short name; ACA ingress port 80) |

CD ([`scripts/cd/azure-cd-build-push.sh`](../scripts/cd/azure-cd-build-push.sh)) builds and pushes
`tangle-study-prometheus` and `tangle-study-grafana` alongside api/web/worker. Deploy
([`azure-cd-deploy-image.sh`](../scripts/cd/azure-cd-deploy-image.sh)) sets GHCR images plus runtime
env from [`azure-aca-urls.sh`](../scripts/cd/libs/azure-aca-urls.sh) (e.g. `PROMETHEUS_URL=http://tangle-study-prometheus`,
`REDIS_URL=redis://tangle-study-redis`).
Secrets: `METRICS_SCRAPE_SECRET`, `GRAFANA_ADMIN_PASSWORD`, and Neon `POSTGRES_CONNECTION_STRING`
(postgres-exporter DSN derived at inject time). See [infra/azure/README.md](azure/README.md#monitoring-on-aca).

Grafana login on Azure: `admin` / `GRAFANA_ADMIN_PASSWORD`. Resolve URL:

```bash
az containerapp show --name tangle-study-grafana --resource-group tangle-study-prod \
  --query properties.configuration.ingress.fqdn -o tsv
```

See [infra/azure/README.md](azure/README.md) and [docs/DEPLOYMENT.md](../docs/DEPLOYMENT.md) for full deploy steps.

## Related docs

- [README.md](../README.md#development-phases) — phased roadmap
- [docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md) — system overview and observability
- [workers/README.md](../workers/README.md) — worker metrics
- [docs/QUEUE.md](../docs/QUEUE.md) — async job contracts
