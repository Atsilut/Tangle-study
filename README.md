# Tangle

## Overview

**Tangle** is a personal study project focused on designing and implementing a scalable, real-time distributed system.
The goal of this project is not commercialization, but to explore practical backend architecture, real-time communication, and DevOps patterns in a production-like environment.

This project combines multiple technologies and languages to simulate a modern service architecture, including API servers, real-time messaging, background workers, and monitoring systems.

---

## Objectives

* Build a **real-time backend system** with chat, social features, and location sharing
* Practice **distributed system design**
* Explore **multi-language architecture** (C#, Rust, Go)
* Implement **DevOps workflows** (CI/CD, containerization, observability)
* Understand trade-offs between **performance, complexity, and scalability**

---

## Core Features

* Community platform (posts, comments, nested replies)
* Friend and group management
* Real-time chat (1:1 and group)
* Media sharing (images/videos via posts and chat)
* Memory Map (location-based content visualization)
* Real-time location sharing with safety alerts

---

## Architecture

**Today:** Nginx edge proxies all `/api/*` and `/hubs/*` to the **gateway** (YARP), which routes to users, media, chat, location, community, group, and social. Optional Rust workers and monitoring. **Azure production** still targets the legacy monolith until Bicep/CD cutover. Full diagrams: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

### High-Level Structure

```
Clients
 ├─ Web (React)
 └─ Mobile (MAUI)
        │
        ▼
   Nginx edge
        │
        ▼
   Gateway (YARP + JWT)
        │
   ┌────┼────────────────────────────┐
   ▼    ▼    ▼    ▼    ▼    ▼    ▼    ▼
Users Media Chat Loc  Comm Group Social
   │    │    │    │    │    │    │    │
   └────┴────┴────┴────┴────┴────┴────┘
        │
 ┌──────┼──────────────┐
 ▼      ▼              ▼
DB    Redis        Queue (Streams)
         │              │
         ▼              ▼
     SignalR       Rust Workers
         │
         ▼
   Real-time Events

Monitoring:
Services / Workers → Prometheus → Grafana
```

Service-layer conventions (one repository per service, peer services for other aggregates): see [docs/AGENTS.md](docs/AGENTS.md).

Service boundaries and MSA migration plan: [docs/SERVICE_BOUNDARIES.md](docs/SERVICE_BOUNDARIES.md), [docs/MSA_MIGRATION.md](docs/MSA_MIGRATION.md).

---

## Tech Stack

### Backend

* **ASP.NET Core**

  * **Users service** — authentication, JWT issuance, user profiles
  * **Gateway** — YARP routing and JWT validation at the edge
  * **Domain services** — media, chat, location, community, group, social business logic

* **PostgreSQL**

  * Primary relational database

---

### Real-Time & Caching

* **Redis**

  * Caching layer
  * Pub/Sub for real-time messaging
  * Streams for lightweight queueing
  * TTL-based storage for location data

* **SignalR**

  * Real-time communication (chat, location updates)

---

### Workers

* **Rust**

  * High-performance background processing
  * Media processing (image/video)
  * Event handling and aggregation
  * Location clustering for Memory Map

---

### DevOps & Tooling

* **Docker**

  * Containerized services

* **GitHub Actions**

  * CI on pull requests and pushes to `main` / `develop` — [`.github/workflows/ci-v2.yml`](.github/workflows/ci-v2.yml)
  * Jobs: .NET build, Rust tests, web lint/test/build, service integration tests (Testcontainers), media harness E2E (Compose + Azurite)
  * Local parity: `./scripts/run-all-tests.sh` (integration + harness + Rust + web; add `--skip-harness` for faster runs)

* **Go**

  * CLI utilities
  * Load testing tools
  * Custom exporters (if needed)

---

### Monitoring

* **Prometheus**

  * Metrics collection

* **Grafana**

  * Visualization dashboards

---

## Design Decisions

### Monorepo Structure

All services, workers, and tools are managed in a single repository for clarity

```
services/
  Gateway/          ← YARP reverse proxy + JWT validation
  Users/            ← Identity, login, JWT issuance (+ Users.Tests)
  Media/            ← Media microservice (+ Media.Tests)
  Chat/             ← Chat microservice (+ Chat.Tests)
  Location/         ← Location microservice (+ Location.Tests)
  Community/        ← Posts/comments microservice (+ Community.Tests)
  Group/            ← Groups microservice (+ Group.Tests)
  Social/           ← Friendships + user-blocks microservice (+ Social.Tests)
  Stack.Tests/      ← Cross-stack harness E2E (Category=Harness)
clients/
  web/              ← React SPA (Phase 6)
workers/
  crates/           ← worker-chat, worker-media, worker-location
infra/              ← Nginx edge, Prometheus, Grafana
docs/               ← architecture and migration hub
scripts/            ← docker-dotnet.sh, ci/docker-test.sh, run-all-tests.sh
```

---

### Technology Separation by Responsibility

* **C# (ASP.NET Core)** → API and business logic
* **Rust** → Performance-critical and asynchronous processing
* **Go** → DevOps tooling
* React -> Frontend

---

### About Message Queue

Redis will be used as:

* Cache
* Real-time messaging backbone
* Lightweight queue (Streams)

To reduce premature complexity while still supporting scalability.

But Kafka or other systems can be introduced later if needed.

---

### Event-Driven Processing

Expected heavy or asynchronous tasks,

```
API → Queue → Rust Worker → Result Storage
```

This improves responsiveness and system scalability.

---

### Monitoring

* Prometheus
* Grafana

---

## Documentation

Central index: [docs/README.md](docs/README.md)

| Doc | Topic |
|-----|-------|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Gateway-centric as-built + remaining Azure gaps |
| [docs/SERVICE_BOUNDARIES.md](docs/SERVICE_BOUNDARIES.md) | Domain → microservice mapping |
| [docs/MSA_MIGRATION.md](docs/MSA_MIGRATION.md) | Extraction order and checklist |
| [services/Gateway/GATEWAY.md](services/Gateway/GATEWAY.md) | YARP routing and JWT validation |
| [services/Users/USERS.md](services/Users/USERS.md) | Identity, login, JWT issuance |
| [clients/web/README.md](clients/web/README.md) | React web client |
| [infra/README.md](infra/README.md) | Prometheus / Grafana monitoring |
| [services/Media/MEDIA.md](services/Media/MEDIA.md) | Media upload and processing |
| [services/Chat/CHAT.md](services/Chat/CHAT.md) | Chat REST routes and SignalR hub |
| [services/Location/LOCATION.md](services/Location/LOCATION.md) | Memory Map, live sharing, safety alerts |
| [services/Group/GROUP.md](services/Group/GROUP.md) | Group service routes and internal contracts |
| [services/Social/SOCIAL.md](services/Social/SOCIAL.md) | Social service (friendships + user-blocks) routes and internal contracts |
| [services/Community/COMMUNITY.md](services/Community/COMMUNITY.md) | Posts/comments service |
| [docs/EVENTS.md](docs/EVENTS.md) | Redis pub/sub event contracts |

---

## Development Phases

| Phase | Focus | Status |
|-------|-------|--------|
| 1 | Core API (auth, community, friends) | Done |
| 2 | Real-time chat (SignalR) — [CHAT.md](services/Chat/CHAT.md) | Done (extracted to chat-service) |
| 3 | Redis (cache + pub/sub + Streams producer) — [QUEUE.md](docs/QUEUE.md) | Done |
| 4 | Rust workers + media on post/comment/chat — [workers README](workers/README.md) | Done |
| 5 | Monitoring (Prometheus / Grafana) — thin stack in [infra/](infra/) | Done |
| 6 | Web client (React) in [clients/web](clients/web/README.md) — backend parity through media | Done |
| 7 | Location / Memory Map — [LOCATION.md](services/Location/LOCATION.md) | Done (extracted to location-service) |
| 8 | MSA prep — cross-service contracts — [GROUP.md](services/Group/GROUP.md), [EVENTS.md](docs/EVENTS.md), [QUEUE.md](docs/QUEUE.md) | Done |
| 9 | MSA migration — all domains + gateway/users cutover in Compose; follow [MSA_MIGRATION.md](docs/MSA_MIGRATION.md) | In progress (Compose complete; Azure cutover pending) |

Phase 9 steps 1–7 (media, chat, location, community, group, social, users + gateway) are **complete in local Compose**. Azure Container Apps + `nginx.production.conf` cutover is the remaining milestone. MAUI remains optional after the React path works.

---

## Disclaimer

This project is built **for learning purposes only**.

* Not optimized for production use
* May include experimental design decisions
* Focused on exploring architecture rather than delivering a finished product

---

## Future Considerations

* Replace Redis Streams with Kafka (if scaling demands it)
* Add distributed tracing and logs (Grafana Alloy + Loki + Tempo)
* Improve fault tolerance and recovery strategies
* Service mesh (beyond gateway + Compose) if operational needs grow

Service decomposition is Phase 9 — see [docs/MSA_MIGRATION.md](docs/MSA_MIGRATION.md).

---

## Local development (Docker only)

All .NET build, test, EF, and API runtime run in Docker so nothing writes `.nuget/` or `.dotnet-tools/` into the repo.

**Prerequisites:** [Docker Desktop](https://www.docker.com/products/docker-desktop/)

**Container versions:** Local `docker compose up` pulls **latest** tags by default. CI and deployment use pinned versions from [`docker/versions.prod.env`](docker/versions.prod.env) — see [`docker/README.md`](docker/README.md).

Shell helpers under `scripts/` use Bash (`chmod +x` on Linux/macOS). On Windows, run the equivalent `docker compose` commands shown below from PowerShell in the repo root.

### Compose services and profiles

| Service | Profile | Always on? | Role |
|---------|---------|------------|------|
| `gateway` | — | yes (default `up`) | YARP reverse proxy + JWT validation |
| `users` | — | yes (default `up`) | Users service (login, JWT issuance) |
| `media` | — | yes | Media microservice (`/api/media/*`) |
| `chat` | — | yes | Chat microservice (`/api/chat/*`, `/hubs/chat`) |
| `location` | — | yes | Location microservice (`/api/location/*`, `/hubs/location`) |
| `community` | — | yes | Posts/comments microservice (`/api/posts/*`, `/api/comments/*`) |
| `group` | — | yes | Groups microservice (`/api/groups/*`, invitations, applications) |
| `social` | — | yes | Friendships and user blocks (`/api/friendships/*`, `/api/users/blocks`) |
| `db` | — | yes | Postgres |
| `redis` | — | yes | Cache, SignalR backplane, Streams |
| `azurite` | — | yes | Local Azure Blob storage (media uploads) |
| `nginx` | — | yes | Edge proxy + React SPA (built in [clients/web/Dockerfile](clients/web/Dockerfile)) |
| `rust-worker-chat` | `workers`, `harness` | no | Chat message worker (`chat.message.created`) |
| `rust-worker-media` | `workers`, `harness` | no | Media upload processor |
| `rust-worker-location` | `workers` | no | Memory Map pin clustering (`location.cluster`) |
| `prometheus`, `postgres-exporter`, `redis-exporter`, `grafana` | `monitoring` | no | Metrics stack |
| `sdk` | `tools` | no | One-off .NET CLI (`run --rm`) |
| `test` | `test` | no | One-off test run (`run --rm`) |
| `harness` | `harness` | no | One-off harness tests (`run --rm`) |

### Default runtime (gateway + services + Postgres + Redis + Azurite + Web)

```bash
docker compose up --build
```

Prod-like local stack (pinned images, same as CI):

```bash
docker compose --env-file docker/versions.prod.env up --build
```

- Web (Nginx + React SPA): http://localhost:8080  
- Swagger: http://localhost:8080/api (via nginx → gateway → users)  
- Postgres: `localhost:5433` (user `tangle`, db `tangledb`)
- Redis: `localhost:6379` (cache, SignalR backplane, pub/sub, Streams producer)
- Azurite (blob): `localhost:10000`

Each service runs EF migrations on startup when `ASPNETCORE_ENVIRONMENT` is `Development` or `Docker`.

Redis details: [REDIS.md](docs/REDIS.md). Chat hub: [CHAT.md](services/Chat/CHAT.md). Location: [LOCATION.md](services/Location/LOCATION.md).

### Manual testing (run the stack, use the UI)

Use this when you want to click through the app in a browser — **not** when running `docker-test.sh` or `run-all-tests.sh` (those start their own containers and do not need `compose up`).

**1. Start containers** (from repo root):

```bash
# App stack: gateway, users, media, chat, location, community, group, social, db, redis, azurite, nginx (React SPA)
docker compose up --build
```

```bash
# Everything above + Rust workers (media processing, chat, map clustering) + Grafana/Prometheus
docker compose --profile workers --profile monitoring up --build
```

Workers are required for media uploads to finish processing and for Memory Map clustering at low zoom. Chat and location realtime need Redis (already in the default stack).

**2. Open the app**

| URL | Use |
|-----|-----|
| http://localhost:8080 | **Primary entry** — Nginx serves the SPA and routes `/api/*` and `/hubs/*` to the gateway |
| http://localhost:3000 | Grafana (`admin` / `admin`) — only with `--profile monitoring` |
| http://localhost:9090 | Prometheus — only with `--profile monitoring` |

Sign up / sign in via the web UI at **:8080**. Postgres is on `localhost:5433` if you need direct DB access.

**3. Smoke-check**

```bash
curl -s http://localhost:8080/health
```

Should return `Healthy` once gateway, users, media, chat, location, and nginx are up.

**4. Feature walkthrough** — see [Phase 7 E2E gate](#phase-7-e2e-gate-memory-map) below for Memory Map; for chat/media, use the main feed and chat screens at :8080.

**5. Reset data** (optional) — [Reset local dev data](#reset-local-dev-data).

**Hot-reload UI dev** (Vite on :5173, same nginx backend): [Web client](#web-client) and [clients/web/README.md](clients/web/README.md).

### Phase 7 E2E gate (Memory Map)

With the default stack running (`docker compose up --build`), verify:

1. **Web** — http://localhost:8080/map loads the basemap and search box.
2. **Health** — `curl -s http://localhost:8080/health` returns `Healthy`.
3. **Pins** — signed-in user double-clicks the map to drop a pin; pin appears after refresh/pan.
4. **Workers** (optional clustering at zoom 2–4): `docker compose --profile workers up -d rust-worker-location`.
5. **Live sharing** — two users in the same group: one starts sharing; the other sees a green marker and sharing status in the member list.
6. **Tests** — `./scripts/ci/docker-test.sh test services/Location.Tests/Location.Tests.csproj -c Release`.

Full UI dev uses Vite at http://localhost:5173 with the same API via Nginx proxy (see [clients/web/README.md](clients/web/README.md)).

### Full stack (workers + monitoring)

Starts the default stack plus both Rust workers and monitoring:

```bash
docker compose --profile workers --profile monitoring up --build
```

- Grafana: http://localhost:3000 (`admin` / `admin`)
- Prometheus: http://localhost:9090

For day-to-day backend work, the default `docker compose up --build` is enough. Add profiles when you need background workers or dashboards.

### Web client

The React app is built into the `nginx` Docker image on `docker compose up --build`. For hot reload during UI work, run Vite on the host. Full setup: [clients/web/README.md](clients/web/README.md).

**Dev (hot reload):**

```bash
# From repo root — backend + Nginx edge (already in default up)
docker compose up gateway users db redis azurite nginx

# From clients/web
npm install
cp .env.example .env
npm run dev            # http://localhost:5173
```

### Reset local dev data

Wipe test users and other application rows from the Compose Postgres (keeps schema and migrations):

```bash
chmod +x scripts/dev-clear-db.sh

./scripts/dev-clear-db.sh          # prompts for confirmation
./scripts/dev-clear-db.sh --yes    # non-interactive
```

Does not clear Redis or Azurite blob storage. Re-run sign-up in the web client after reset.

### Build / EF / other CLI

```bash
chmod +x scripts/docker-dotnet.sh

./scripts/docker-dotnet.sh build Tangle.slnx
./scripts/docker-dotnet.sh ef migrations add MyMigration --project services/Users
./scripts/docker-dotnet.sh ef database update --project services/Users
```

Equivalent without the script:

```bash
docker compose --profile tools run --rm sdk build Tangle.slnx
docker compose --profile tools run --rm sdk ef migrations add MyMigration --project services/Users
```

### Tests (Docker Compose)

**Prerequisite:** [Docker Desktop](https://www.docker.com/products/docker-desktop/) running.

Integration tests and harness E2E both use Docker Compose, but they work differently:

| Suite | Needs `docker compose up`? | What it does |
|-------|------------------------------|--------------|
| **Integration** (Users, Media, Chat, Location, Community, Group, Social) | **No** | `test` service runs `dotnet test` in a container with the host Docker socket; Testcontainers starts its own Postgres/Redis |
| **Harness E2E** (`Category=Harness`) | **No** (script starts stack) | `run-stack-harness.sh` builds stack images, runs `compose up`, then runs harness tests against nginx (`HARNESS_MODULES` filter) |
| **Dev stack** (`docker compose up`) | N/A | Manual API/web testing — not a substitute for the test suites above |

Use `scripts/ci/docker-test.sh` or `--profile test` for integration tests. Do **not** use `docker-dotnet.sh` / `sdk` — the `test` service mounts `/var/run/docker.sock` for Testcontainers.

**CI parity:** [`.github/workflows/ci-v2.yml`](.github/workflows/ci-v2.yml).

#### Quick start — integration tests only

Fastest local run (builds test projects inside the `test` container; Users + Media + Chat + Location):

```bash
chmod +x scripts/ci/docker-test.sh

./scripts/ci/docker-test.sh
```

Equivalent:

```bash
docker compose --profile test run --rm test
```

Run **all** service integration projects (matches CI after publish):

```bash
chmod +x scripts/ci/dotnet-publish.sh scripts/ci/docker-test.sh

./scripts/ci/dotnet-publish.sh

./scripts/ci/docker-test.sh test services/Users.Tests/Users.Tests.csproj -c Release --no-build --filter "Category!=Harness"
./scripts/ci/docker-test.sh test services/Media.Tests/Media.Tests.csproj -c Release --no-build
./scripts/ci/docker-test.sh test services/Chat.Tests/Chat.Tests.csproj -c Release --no-build
./scripts/ci/docker-test.sh test services/Location.Tests/Location.Tests.csproj -c Release --no-build
```

Single project or filter:

```bash
./scripts/ci/docker-test.sh test services/Users.Tests/Users.Tests.csproj -c Release --filter "FullyQualifiedName~UserNicknameCacheRedisIntegrationTests"
./scripts/ci/docker-test.sh test services/Location.Tests/Location.Tests.csproj -c Release --filter "FullyQualifiedName~MapPin"
```

#### Full local test run (integration + harness + Rust + web)

```bash
chmod +x scripts/run-all-tests.sh

./scripts/run-all-tests.sh

# Skip slow harness during day-to-day runs
./scripts/run-all-tests.sh --skip-harness

# Rust + web in parallel, then integration + harness
./scripts/run-all-tests.sh --parallel-frontend
```

`run-all-tests.sh` calls `docker-test.sh` with no args (Users + Media + Chat + Location + Community + Group + Social integration tests). Pass extra args after `--` to filter:

```bash
./scripts/run-all-tests.sh -- --filter "FullyQualifiedName~MetricsIntegrationTests"
```

#### Stack harness E2E

Starts its own Compose stack (gateway, domain services, nginx, workers) — you do **not** need `docker compose up` running first:

```bash
chmod +x scripts/ci/run-stack-harness.sh

# All modules (CI default)
HARNESS_MODULES=all ./scripts/ci/run-stack-harness.sh

# Media only
./scripts/ci/run-media-harness.sh
```

#### Notes

- The compose `db` service is **not** required for integration tests (Testcontainers provides Postgres).
- Harness E2E (`Category=Harness` in `Stack.Tests`) runs only through `run-stack-harness.sh`; filter with `HARNESS_MODULES` (`users`, `social`, `group`, `community`, `media`, `chat`, `location`, `all`).
- **Matrix vs harness:** TheoryData matrices and internal-API tests stay in `{Service}.Tests`; gateway JWT, SignalR via nginx, rust-workers, and real cross-service HTTP belong in `Stack.Tests`. See [docs/MSA_MIGRATION.md — Matrix vs harness](docs/MSA_MIGRATION.md#matrix-vs-harness-where-tests-belong).
- Service integration tests disable Redis in-process by default; realtime tests use a Testcontainers Redis — see [REDIS.md](docs/REDIS.md).

### Rust workers (optional, `workers` profile)

Three dedicated worker binaries (chat, media, location):

| Service | Stream | Image (CI/CD) |
|---------|--------|----------------|
| `rust-worker-chat` | `chat.message.created` | `tangle-study-worker-chat` |
| `rust-worker-media` | `media.uploaded` | `tangle-study-worker-media` |
| `rust-worker-location` | `location.cluster` | `tangle-study-worker-location` |

Local `docker compose --profile workers up` uses multi-stage builds. CI/CD compile once via `./scripts/ci/build-workers-release.sh` and build slim runtime images with `./scripts/ci/build-worker-images.sh`. See [workers/README.md](workers/README.md).

Requires Redis, domain services (media, chat, location), and Azurite for media jobs. See [workers/README.md](workers/README.md) and [LOCATION.md](services/Location/LOCATION.md).

```bash
# Chat worker only (with core stack)
docker compose --profile workers up --build rust-worker-chat

# All workers + core stack
docker compose --profile workers up --build
```

### Monitoring (optional, `monitoring` profile)

Prometheus and Grafana use the Compose `monitoring` profile. See [infra/README.md](infra/README.md).

```bash
# Services + exporters + Prometheus + Grafana
docker compose --profile monitoring up --build

# Include worker scrape targets (needs `workers` profile)
docker compose --profile monitoring --profile workers up --build
```

- Grafana: http://localhost:3000 (`admin` / `admin`)
- Prometheus: http://localhost:9090
- Service metrics: scraped by Prometheus on the Compose network (see [infra/README.md](infra/README.md))

### Harness tests (optional, `harness` profile)

End-to-end tests that run inside Compose against nginx (real JWT via gateway/users, cross-service flows). Prefer `./scripts/ci/run-stack-harness.sh` — it builds images, starts the stack, and sets harness env vars:

```bash
HARNESS_MODULES=all ./scripts/ci/run-stack-harness.sh
HARNESS_MODULES=chat ./scripts/ci/run-stack-harness.sh
```

### Cleanup local SDK artifacts

If `.nuget/` or `.dotnet-tools/` were created in the repo earlier, delete them (they are gitignored):

```bash
rm -rf .nuget .dotnet-tools
```

---

## Summary

Tangle is an exploration of:

* Real-time systems
* Distributed architecture
* Multi-language backend design
* Practical DevOps workflows

The project prioritizes **learning depth and architectural clarity** over completeness or production readiness.
