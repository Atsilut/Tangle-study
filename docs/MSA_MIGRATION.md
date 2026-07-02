# MSA migration

How Tangle moves from a modular monolith to domain-aligned microservices.

Related: [ARCHITECTURE.md](ARCHITECTURE.md) (as-built diagram), [SERVICE_BOUNDARIES.md](SERVICE_BOUNDARIES.md) (domain map and FK notes), [DEPLOYMENT.md](DEPLOYMENT.md) (Azure CD).

---

## Branch and deploy model

| Branch | CI | Deploy |
|--------|----|--------|
| **`develop`** | Full CI ladder (see below) | None — safe place for MSA work |
| **`main`** | CI | Production CD (runtime images → GHCR) |

MSA extraction lands on **`develop`** first. Azure production still serves all `/api/*` from the monolith until media Container App + `nginx.production.conf` strangler ship on **`main`**.

---

## CI ladder (`ci-v1.yml`)

Build once, test many times — harness reuses compiled artifacts instead of rebuilding inside the harness job.

```text
Tier 1 (parallel)
  dotnet-build   → dotnet-publish.sh (Api + Media publish, test projects built)
  rust           → build-workers-release.sh (cargo test + release binaries)
  web            → lint, test, build

Tier 2 (needs dotnet-build)
  api-tests      → Api.Tests --no-build (Category!=Harness)
  media-tests    → Media.Tests --no-build

Tier 3 (needs dotnet-build, rust, api-tests, media-tests)
  compose-build  → compose-build-stack.sh → harness-stack.tar artifact

Tier 4 (needs compose-build)
  harness-media  → load stack tar, compose up --no-build, Category=Harness tests
```

| Job | Produces | Harness reuses? |
|-----|----------|-----------------|
| `dotnet-build` | `.ci-cache/publish/{api,media}`, test `bin/` | Yes — runtime Api/Media Dockerfiles |
| `rust` | `workers/target/release/*` | Yes — worker runtime images |
| `api-tests` / `media-tests` | Pass/fail | Gate only |
| `compose-build` | `api`, `media`, `nginx`, `rust-worker-media` images + tar | Yes — harness loads tar |
| `harness-media` | E2E pass/fail | — |

**PR path filters:** integration jobs run only when their service paths change; harness stack build runs when harness-related paths change (or on every push to `main`/`develop`).

**Local parity:**

```bash
./scripts/ci/dotnet-publish.sh
./scripts/ci/build-workers-release.sh
./scripts/ci/compose-build-stack.sh    # optional: saves harness-stack.tar
./scripts/ci/run-media-harness.sh      # full pipeline end-to-end
./scripts/ci/docker-test.sh            # after dotnet-publish; add --no-build to match CI
```

**Runtime images:** CI/CD and harness use `docker-compose.runtime.yml` (slim Dockerfiles COPY prebuilt binaries). Local `docker compose up --build` still uses multi-stage Dockerfiles for ad-hoc dev.

**CD** mirrors CI compile step: `azure-cd-build-push.sh` runs `dotnet-publish.sh` + `build-workers-release.sh`, then pushes runtime images (`tangle-study-api`, three worker tags, web, monitoring).

### Adding the next service

1. Add `services/{Service}/Dockerfile.runtime` + publish output in `dotnet-publish.sh`
2. Add `{Service}.Tests` job (or extend path filters on `media-tests`-style job)
3. Include service image in `compose-build-stack.sh` / `harness-stack.tar` when harness needs it
4. Add GHCR image to `azure-cd-build-push.sh` + `parameters.prod.json` when Azure ships

---

## Extraction order

```text
1. media      ← done on develop (Compose)
2. chat
3. location
4. posts + comments
5. groups
6. friendships + user-blocks
7. users + gateway
```

Rationale: start with services that already have async/realtime boundaries (media, chat), leave identity and the gateway for last.

---

## Workflow (per service)

Use this loop on **`develop`** for each extraction. Step 1 (media) followed it end-to-end.

```text
1. Extract   → services/{Service}/, schema/migrations, Dockerfile, compose + nginx routes
2. Test      → services/{Service}.Tests (unit + integration); keep boundary tests in Api.Tests
3. Wire Api  → HTTP client (e.g. IMediaClient), InternalAccess callbacks; require service URL at startup
4. Remove    → delete monolith Domain/{Name}/*, drop public tables, EF migration
5. Test again → Api.Tests + harness E2E through nginx
6. Docs      → ARCHITECTURE, SERVICE_BOUNDARIES, this file
7. Merge     → develop → main when Azure cutover is ready (media: pending)
```

**Test split**

| Project | Owns |
|---------|------|
| `{Service}.Tests` | Service routes, limits, worker callbacks, internal APIs |
| `Api.Tests` | Monolith boundary (`Fake*Client`), cross-domain attach flows, stack harness |

---

## Step 1 — media-service (`develop`: done)

### Runtime (local Compose)

```text
Browser → nginx:8080
  ├─ /api/media/*, /internal/media/*  → media:8080
  └─ /api/*, /hubs/*                  → api:8080

api ──HttpMediaClient──► media (/internal/media/*)
media ──HttpMonolithAccessClient──► api (/internal/access/*)
rust-worker-media ──PATCH──► media (/internal/media/{id}/processed)
```

Postgres: one instance — `public` schema (monolith), `media` schema (media-service). Redis: `media.uploaded` stream → worker.

### Completed on develop

| Item | Status |
|------|--------|
| `services/Media/` | Done |
| `media` schema + `MediaDbContext` migrations | Done |
| Nginx strangler + compose `media` service | Done |
| `IMediaClient` / `IMonolithAccessClient` | Done |
| `rust-worker-media` → media callbacks | Done |
| Monolith cleanup (`Domain/Media`, `public."MediaAssets"`) | Done |
| `Media.Tests` + `Api.Tests` boundary/harness | Done |
| `Media:Enabled` toggle removed — blob storage required at startup | Done |

### Still open (before `main` cutover)

| Item | Notes |
|------|-------|
| Azure media Container App | CD + Bicep/parameters |
| `nginx.production.conf` media upstream | Strangler for prod |
| Worker `API_BASE_URL` on Azure | Point at media-service, not monolith |
| Gateway JWT (step 7) | Media still validates bearer tokens interim |

**Dev data:** uploads live in `media."MediaAssets"`. Legacy `public."MediaAssets"` is gone — reset the Compose DB volume if you need a clean slate.

### Local commands

```bash
# Stack (media + monolith + nginx) — multi-stage dev build
docker compose up --build

# Workers + monitoring (optional)
docker compose --profile workers --profile monitoring up --build

# CI-like: publish + integration tests (--no-build matches CI after dotnet-publish)
./scripts/ci/dotnet-publish.sh
./scripts/ci/docker-test.sh test services/Api.Tests/Api.Tests.csproj -c Release --no-build --filter "Category!=Harness"
./scripts/ci/docker-test.sh test services/Media.Tests/Media.Tests.csproj -c Release --no-build

# Media harness E2E (runtime images via docker-compose.runtime.yml)
./scripts/ci/run-media-harness.sh
```

---

## Steps 2–7

Not started. Reuse the [workflow](#workflow-per-service) above; FK and cross-route notes live in [SERVICE_BOUNDARIES.md](SERVICE_BOUNDARIES.md).

---

## Strangler edge

| Environment | Edge | Media routes |
|-------------|------|--------------|
| **Compose (`develop`)** | `infra/nginx/nginx.conf` | `/api/media/*` → `media:8080` |
| **Azure (`main`)** | `infra/nginx/nginx.production.conf` | Still monolith-only until cutover |

Target end state: gateway validates JWT once; services receive identity claims. Until step 7, each service validates bearer tokens with the shared `Jwt:Secret`.
