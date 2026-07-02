# MSA migration

How Tangle moves from a modular monolith to domain-aligned microservices.

Related: [ARCHITECTURE.md](ARCHITECTURE.md) (as-built diagram), [SERVICE_BOUNDARIES.md](SERVICE_BOUNDARIES.md) (domain map and FK notes), [DEPLOYMENT.md](DEPLOYMENT.md) (Azure CD).

---

## Branch and deploy model

| Branch | CI | Deploy |
|--------|----|--------|
| **`develop`** | Full CI (Api + Media tests, harness) | None — safe place for MSA work |
| **`main`** | CI | Production CD (monolith-only today) |

MSA extraction lands on **`develop`** first. Azure production still serves all `/api/*` from the monolith until media Container App + `nginx.production.conf` strangler ship on **`main`**.

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
# Stack (media + monolith + nginx)
docker compose up --build

# Workers + monitoring (optional)
docker compose --profile workers --profile monitoring up --build

# Tests (Testcontainers)
./scripts/ci/docker-test.sh

# Media harness E2E (nginx → media → worker → azurite)
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
