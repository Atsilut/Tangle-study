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
    recording_rules.yml              # precomputed tangle:api_* metrics
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

Monitoring uses the Compose `monitoring` profile. The API must be running for the `api` scrape target; workers need the `workers` profile.

```bash
# API + Prometheus + Grafana + infra exporters
docker compose --profile monitoring up --build

# Include Rust workers (chat + media scrape targets)
docker compose --profile monitoring --profile workers up --build
```

## URLs (host)

| Service | URL | Notes |
|---------|-----|-------|
| Grafana | http://localhost:3000 | Login: `admin` / `admin` (dev default) |
| Prometheus | http://localhost:9090 | Targets page shows scrape health |
| API health | http://localhost:5000/health | Plain-text `Healthy` / `Unhealthy` (Compose healthcheck uses status code) |
| API metrics | http://localhost:5000/metrics | Requires `X-Metrics-Secret` in Docker (see below) |
| Worker metrics | in-container `:9090/metrics` | Scraped by Prometheus on the Compose network |

## Scrape targets

Core jobs in [prometheus/prometheus.yml](prometheus/prometheus.yml). Worker jobs in [prometheus/scrape/workers.yml](prometheus/scrape/workers.yml) are loaded at Prometheus startup only when the Compose `workers` profile is active (see [docker-entrypoint.sh](prometheus/docker-entrypoint.sh)).

| Job | Target | Metrics |
|-----|--------|---------|
| `api` | `api:8080` | HTTP request rate, latency, status codes; health gauges; work queue enqueue counter |
| `rust-worker-chat` | `rust-worker:9090` | Job processing, pending queue, DLQ length, callback responses (`workers` profile) |
| `rust-worker-media` | `rust-worker-media:9090` | Same (`workers` profile) |
| `postgres` | `postgres-exporter:9187` | Connection counts, settings, activity |
| `redis` | `redis-exporter:9121` | Memory, clients, uptime |

Without `--profile workers`, worker jobs are **not scraped** (no targets, no false `WorkerScrapeTargetDown` alert). With `--profile workers`, both worker targets should be **UP** when healthy.

Optional host debug ports (not mapped by default): exec into a worker container and `wget -qO- http://127.0.0.1:9090/metrics`.

## API `/health`

`GET /health` returns a plain-text aggregate result (`Healthy` or `Unhealthy`) from ASP.NET Core health checks for PostgreSQL and Redis (when Redis is enabled). The API Compose healthcheck probes this endpoint for the status code only.

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
| `tangle:api_request_rate` | Traffic guard for ratio/latency alerts |
| `tangle:api_5xx_ratio` | 5xx share of total requests |
| `tangle:api_other_4xx_ratio` | 4xx excluding 401/403/409 |
| `tangle:api_5xx_rate_by_controller` | Per-controller 5xx rate |
| `tangle:api_request_duration_p95` | API latency SLO input |

## Dashboard

**Tangle Overview** (`uid: tangle-overview`) is auto-provisioned under the **Tangle** folder in Grafana:

- API request rate and latency (p50 / p95 with 2s threshold line)
- API 4xx error rate by status code
- API 5xx and other-4xx ratios (recording rules)
- API errors by controller; top 5xx controllers
- Worker jobs processed by outcome
- Worker callback responses by code
- Worker pending messages and DLQ length
- Work queue enqueue rate from the API
- Postgres connections and Redis memory/clients

Panels use metrics from `prometheus-net` (API) and custom `tangle_*` counters/gauges (workers).

## Alerting

Provisioned under `grafana/provisioning/alerting/`. All rules use `for: 5m` before firing. View in Grafana → **Alerting** → **Alert rules** (folder: **Tangle**).

No Alertmanager, Slack, or email — alerts appear in the Grafana UI only.

### API HTTP (`rules-api-http.yml`)

| Rule | Condition | Severity |
|------|-----------|----------|
| `ApiScrapeTargetDown` | `up{job="api"} == 0` | critical |
| `ApiDependencyUnhealthy` | `min(aspnetcore_healthcheck_status{job="api"}) == 0` | critical |
| `ApiHigh5xxRate` | 5xx rate > **0.1 req/s** | critical |
| `ApiHigh5xxRatio` | `tangle:api_5xx_ratio > 1%` and traffic > 0.05 req/s | critical |
| `ApiHigh401Rate` | 401 rate > **0.5 req/s** | warning |
| `ApiHigh403Rate` | 403 rate > **0.1 req/s** | warning |
| `ApiHigh409Rate` | 409 rate > **0.5 req/s** | warning |
| `ApiHighOther4xxRatio` | `tangle:api_other_4xx_ratio > 10%` and traffic > 0.05 req/s | warning |
| `ApiHighLatencyP95` | `tangle:api_request_duration_p95 > 2s` and traffic > 0.05 req/s | warning |
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
2. **Dependency unhealthy** — `docker compose logs api db redis`; verify `/health`.
3. **5xx spike** — Grafana dashboard → errors by controller; check API logs.
4. **Worker DLQ** — `cargo run -- replay` or inspect `{stream}.dlq` in Redis.
5. **Callback 5xx** — media worker cannot reach API; check `API_BASE_URL` and `WORKER_CALLBACK_SECRET`.

## Verification

```bash
# Full stack
docker compose --profile monitoring --profile workers up --build

# Prometheus targets (api, postgres, redis, workers) should be UP
open http://localhost:9090/targets

# Health (plain text)
curl -s http://localhost:5000/health

# Per-dependency health gauges (Prometheus)
curl -s -H "X-Metrics-Secret: dev-metrics-secret" http://localhost:5000/metrics | grep aspnetcore_healthcheck_status

# Metrics endpoint auth (Docker)
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5000/metrics          # expect 401
curl -s -o /dev/null -w "%{http_code}\n" -H "X-Metrics-Secret: dev-metrics-secret" http://localhost:5000/metrics  # expect 200

# Integration tests (Testcontainers — use `test` profile, not `docker-dotnet.sh` / `sdk`)
./scripts/docker-test.sh test services/Api.Tests/Api.Tests.csproj -c Release --filter "FullyQualifiedName~MetricsIntegrationTests|FullyQualifiedName~MetricsScrapeAuthIntegrationTests"
```

## Azure Container Apps (production)

The same Prometheus/Grafana provisioning (`infra/grafana/provisioning/`, `infra/prometheus/recording_rules.yml`) is bundled into custom GHCR images for ACA:

```
infra/azure/monitoring/
  prometheus/Dockerfile          # ACA scrape config at container start
  prometheus/prometheus-start.sh # internal FQDN targets + X-Metrics-Secret
  grafana/Dockerfile             # sed Prometheus datasource to internal URL
  grafana/grafana-start.sh
```

| Compose (local) | Azure ACA |
|-----------------|-----------|
| `prometheus:9090` (host) | `tangle-study-prometheus` (internal) |
| `grafana:3000` (host) | `tangle-study-grafana` (external FQDN) |
| `postgres-exporter` → compose `db` | `tangle-study-postgres-exporter` → **Neon** |
| `redis-exporter` → compose `redis` | `tangle-study-redis-exporter` → internal Redis |
| `api:8080/metrics` | `tangle-study-api.internal.<domain>:8080/metrics` |
| `rust-worker-*:9090/metrics` | `tangle-study-worker-*.internal.<domain>:9090/metrics` |

CD builds and deploys `tangle-study-prometheus` and `tangle-study-grafana` alongside app images. Secrets: `METRICS_SCRAPE_SECRET`, `GRAFANA_ADMIN_PASSWORD`, and Neon `POSTGRES_CONNECTION_STRING` (postgres-exporter DSN derived at inject time).

Grafana login on Azure: `admin` / `GRAFANA_ADMIN_PASSWORD`. Resolve URL:

```bash
az containerapp show --name tangle-study-grafana --resource-group tangle-study-prod \
  --query properties.configuration.ingress.fqdn -o tsv
```

See [infra/azure/README.md](azure/README.md) and [docs/DEPLOYMENT.md](../docs/DEPLOYMENT.md) for full deploy steps.

## Related docs

- [README.md](../README.md#development-phases) — phased roadmap
- [docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md) — system overview and observability
- [workers/rust-worker/README.md](../workers/rust-worker/README.md) — worker metrics
- [services/Api/Global/Queue/QUEUE.md](../services/Api/Global/Queue/QUEUE.md) — async job contracts
