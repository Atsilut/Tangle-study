# MSA migration

How Tangle moves from a modular monolith to domain-aligned microservices.

Related: [ARCHITECTURE.md](ARCHITECTURE.md) (as-built diagram), [SERVICE_BOUNDARIES.md](SERVICE_BOUNDARIES.md) (domain map and FK notes), [DEPLOYMENT.md](DEPLOYMENT.md) (Azure CD).

---

## Branch and deploy model

| Branch | CI | Deploy |
|--------|----|--------|
| **`develop`** | Full CI ladder (see below) | None — safe place for MSA work |
| **`main`** | CI | Production CD (runtime images → GHCR) |

MSA extraction lands on **`develop`** first. Azure production still serves all `/api/*` from the monolith until gateway/users + domain Container Apps and `nginx.production.conf` cutover ship on **`main`**.

---

## CI ladder (`ci-v1.yml`)

Build once, test many times — harness reuses compiled artifacts instead of rebuilding inside the harness job.

```text
Tier 1 (parallel)
  dotnet-build   → dotnet-publish.sh (Gateway + Users + Media + Chat + Location + Community + Group + Social publish, service test projects built)
  rust           → build-workers-release.sh (cargo test + release binaries)
  web            → lint, test, build

Tier 2 (needs dotnet-build)
  users-tests    → Users.Tests --no-build
  media-tests    → Media.Tests --no-build
  chat-tests     → Chat.Tests --no-build
  location-tests → Location.Tests --no-build
  community-tests → Community.Tests --no-build
  group-tests    → Group.Tests --no-build
  social-tests   → Social.Tests --no-build

Tier 3 (needs dotnet-build, rust, users-tests, media-tests, chat-tests, location-tests, community-tests, group-tests, social-tests)
  compose-build  → compose-build-stack.sh → harness-stack.tar artifact

Tier 4 (needs compose-build)
  harness-media  → load stack tar, compose up --no-build, Api.Tests Category=Harness E2E
```

| Job | Produces | Harness reuses? |
|-----|----------|-----------------|
| `dotnet-build` | `.ci-cache/publish/{gateway,users,media,chat,location,community,group,social}`, service test `bin/` | Yes — runtime service Dockerfiles |
| `rust` | `workers/target/release/*` | Yes — worker runtime images |
| `users-tests` / `media-tests` / `chat-tests` / `location-tests` / `community-tests` / `group-tests` / `social-tests` | Pass/fail | Gate only |
| `compose-build` | `gateway`, `users`, `media`, `nginx`, `rust-worker-media`, `harness` images + tar | Yes — harness loads tar |
| `harness-media` | E2E pass/fail | — |

**PR path filters:** integration jobs run when their service paths change; Gateway/Users changes (`dotnet-api`) also trigger downstream service test jobs that depend on identity/routing. Harness stack build runs when harness-related paths change (or on every push to `main`/`develop`).

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
2. chat       ← done on develop (Compose)
3. location   ← done on develop (Compose)
4. community (posts + comments)  ← done on develop (Compose)
5. group                         ← done on develop (Compose)
6. social (friendships + user-blocks) ← done on develop (Compose)
7. users + gateway ← done on develop (Compose)
```

Rationale: start with services that already have async/realtime boundaries (media, chat), leave identity and the gateway for last.

---

## Workflow (per service)

Use this loop on **`develop`** for each extraction. Step 1 (media) followed it end-to-end. After step 7, services use **`IUserClient`** and **gateway identity auth** instead of monolith internal access.

```text
1. Extract   → services/{Service}/, schema/migrations, Dockerfile, compose + gateway YARP routes
2. Test      → services/{Service}.Tests (unit + integration)
3. Wire      → HTTP clients (e.g. IMediaClient, IUserClient); require service URL at startup
4. Remove    → delete monolith Domain/{Name}/*, drop public tables, EF migration
5. Test again → {Service}.Tests + harness E2E through nginx → gateway
6. Docs      → ARCHITECTURE, SERVICE_BOUNDARIES, this file
7. Merge     → develop → main when Azure cutover is ready
```

**Test split**

| Project | Owns |
|---------|------|
| `{Service}.Tests` | Service routes, limits, worker callbacks, internal APIs |
| `Api.Tests` | Stack harness E2E only (`Category=Harness`) |

---

## Step 1 — media-service (`develop`: done)

### Runtime (local Compose)

Current routing (post step 7). Service-specific paths are handled by gateway YARP — see Step 7 below.

```text
Browser → nginx:8080 → gateway:8080 → media:8080
community / chat ──IMediaClient──► media (/internal/media/*)
media ──IUserClient──► users (/internal/users/*)
rust-worker-media ──PATCH──► media (/internal/media/{id}/processed)
```

Postgres: one instance — `public` schema (monolith), `media` schema (media-service). Redis: `media.uploaded` stream → worker.

### Completed on develop

| Item | Status |
|------|--------|
| `services/Media/` | Done |
| `media` schema + `MediaDbContext` migrations | Done |
| Nginx strangler + compose `media` service | Done |
| `IMediaClient` / `IUserClient` | Done |
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

**Dev data:** uploads live in `media."MediaAssets"`. Legacy `public."MediaAssets"` is gone — reset the Compose DB volume if you need a clean slate.

---

## Step 2 — chat-service (`develop`: done)

### Runtime (local Compose)

```text
Browser → nginx:8080 → gateway:8080 → chat:8080
users ──IChatClient──► chat (/internal/chat/users/{id}/detach-on-deletion)
media ──HttpChatAccessClient──► chat (/internal/chat/messages/{id}/media-view)
chat ──IUserClient──► users (/internal/users/*)
chat ──IMediaClient──► media (/internal/media/*)
rust-worker-chat ──POST──► chat (chat.message.created callback)
```

Postgres: one instance — `public` schema (monolith), `media` schema (media-service), `chat` schema (chat-service). Redis: `chat.message.created` stream → `rust-worker-chat`.

### Completed on develop

| Item | Status |
|------|--------|
| `services/Chat/` | Done |
| `chat` schema + `ChatDbContext` migrations | Done |
| Nginx strangler + compose `chat` service | Done |
| `IChatClient` / `IChatAccessClient` / `IUserClient` | Done |
| `rust-worker-chat` → chat callbacks | Done |
| Monolith cleanup (`Domain/Chat`, public chat tables) | Done |
| `Chat.Tests` + `Api.Tests` boundary tests | Done |

### Still open (before `main` cutover)

| Item | Notes |
|------|-------|
| Azure chat Container App | CD + Bicep/parameters |
| `nginx.production.conf` chat upstream | Strangler for prod |
| Worker `API_BASE_URL` on Azure | Point at chat-service, not monolith |

**Dev data:** chat rows live in `chat` schema. Legacy `public."Chat*"` tables are dropped by `RemoveMonolithChatTables` — reset the Compose DB volume if you need a clean slate.

### Local commands

```bash
# Full gateway stack — multi-stage dev build
docker compose up --build

# Workers + monitoring (optional)
docker compose --profile workers --profile monitoring up --build

# CI-like: publish + integration tests (--no-build matches CI after dotnet-publish)
./scripts/ci/dotnet-publish.sh
./scripts/ci/docker-test.sh test services/Users.Tests/Users.Tests.csproj -c Release --no-build
./scripts/ci/docker-test.sh test services/Media.Tests/Media.Tests.csproj -c Release --no-build
./scripts/ci/docker-test.sh test services/Chat.Tests/Chat.Tests.csproj -c Release --no-build

# Media harness E2E (runtime images via docker-compose.runtime.yml)
./scripts/ci/run-media-harness.sh
```

---

## Step 3 — location-service (`develop`: done)

### Runtime (local Compose)

```text
Browser → nginx:8080 → gateway:8080 → location:8080
users ──ILocationClient──► location (/internal/location/*)
location ──IUserClient──► users (/internal/users/*)
rust-worker-location ──GET/PUT──► location (/internal/location/cluster-*)
```

Postgres: one instance — `public` schema (monolith), `media`, `chat`, and `location` schemas. Redis: `location.cluster` stream → `rust-worker-location`.

### Completed on develop

| Item | Status |
|------|--------|
| `services/Location/` | Done |
| `location` schema + `LocationDbContext` migrations | Done |
| Nginx strangler + compose `location` service | Done |
| `ILocationClient` / `IUserClient` | Done |
| `rust-worker-location` → location callbacks (`API_BASE_URL=http://location:8080`) | Done |
| Monolith cleanup (`Domain/Location`, public location tables) | Done |
| `Location.Tests` + `Api.Tests` boundary tests (`FakeLocationClient`) | Done |

### Still open (before `main` cutover)

| Item | Notes |
|------|-------|
| Azure location Container App | CD + Bicep/parameters |
| `nginx.production.conf` location upstream | Strangler for prod |
| Worker `API_BASE_URL` on Azure | Point at location-service, not monolith |

**Dev data:** pins and sessions live in `location` schema. Legacy `public."MapPins"` / `public."LocationSessions"` are dropped by `RemoveMonolithLocationTables` — reset the Compose DB volume if you need a clean slate.

### Local commands

```bash
# Full gateway stack — multi-stage dev build
docker compose up --build

# CI-like: publish + integration tests (--no-build matches CI after dotnet-publish)
./scripts/ci/dotnet-publish.sh
./scripts/ci/docker-test.sh test services/Users.Tests/Users.Tests.csproj -c Release --no-build
./scripts/ci/docker-test.sh test services/Media.Tests/Media.Tests.csproj -c Release --no-build
./scripts/ci/docker-test.sh test services/Chat.Tests/Chat.Tests.csproj -c Release --no-build
./scripts/ci/docker-test.sh test services/Location.Tests/Location.Tests.csproj -c Release --no-build
```

---

## Step 4 — community-service (posts + comments) (`develop`: done)

One service owns both aggregates (tight delete/detach and board-visibility coupling). API reference: [COMMUNITY.md](../services/Community/COMMUNITY.md).

### Runtime (local Compose)

```text
Browser → nginx:8080 → gateway:8080 → community:8080
users ──ICommunityClient──► community (detach, delete-by-group)
media ──ICommunityAccessClient──► community (post/comment media-view)
location ──ICommunityAccessClient──► community (owner, viewable-ids)
community ──IUserClient──► users (users, blocks, board access)
community ──IMediaClient──► media
community ──ILocationClient──► location
```

Postgres: `community` schema (`Posts`, `Comments`). Legacy `public."Posts"` / `public."Comments"` dropped by `RemoveMonolithCommunityTables`.

### Still open (before `main` cutover)

| Item | Notes |
|------|-------|
| Azure community Container App | CD + Bicep/parameters |
| `nginx.production.conf` community upstream | Strangler for prod |

## Step 5 — group-service (`develop`: done)

One service owns groups, boards, memberships, invitations, applications, and blacklist. API reference: [GROUP.md](../services/Group/GROUP.md).

### Runtime (local Compose)

```text
Browser → nginx:8080 → gateway:8080 → group:8080
users ──IGroupClient──► group (user detach)
community ──IGroupClient──► group (board access)
chat ──IGroupClient──► group (membership)
location ──IGroupClient──► group (membership)
group ──IUserClient──► users (users, blocks)
group ──ICommunityClient──► community (delete-all)
group ──ILocationClient──► location (end-sessions)
```

Postgres: `group` schema. Legacy `public` group tables dropped by `RemoveMonolithGroupTables` **without copying rows**. Local Compose stacks must wipe the Postgres volume (`docker compose down -v`) when upgrading an existing DB; see [GROUP.md](../services/Group/GROUP.md).

### Still open (before `main` cutover)

| Item | Notes |
|------|-------|
| Azure group Container App | CD + Bicep/parameters |
| `nginx.production.conf` group upstream | Strangler for prod |

## Step 6 — social-service (friendships + user-blocks) (`develop`: done)

One service owns friendships, friend requests, and user-blocks (tight block ↔ request coupling via `IgnoredByBlock`). API reference: [SOCIAL.md](../services/Social/SOCIAL.md).

### Runtime (local Compose)

```text
Browser → nginx:8080 → gateway:8080 → social:8080
users ──ISocialClient──► social (user detach)
chat / group / community / location ──ISocialClient──► social (friendship + block checks)
social ──IUserClient──► users (users, nicknames, friends-list visibility)
```

Postgres: `social` schema (`Friendships`, `FriendRequests`, `UserBlocks`). Legacy `public` social tables dropped by `RemoveMonolithSocialTables` **without copying rows**. Local Compose stacks must wipe the Postgres volume (`docker compose down -v`) when upgrading an existing DB; see [SOCIAL.md](../services/Social/SOCIAL.md).

### Still open (before `main` cutover)

| Item | Notes |
|------|-------|
| Azure social Container App | CD + Bicep/parameters |
| `nginx.production.conf` social upstream | Strangler for prod |

## Step 7 — users-service + gateway (`develop`: Compose done; Azure open)

Users and gateway are **separate deployables** (not combined). Users owns identity; gateway owns routing and JWT validation. **Compose dev stack is complete**; Azure/CD and `nginx.production.conf` remain open.

### users-service

- Source: former `services/Api/Domain/Users/`
- Postgres: `users` schema (`Users` table)
- Public routes: `/api/users/*`, `/api/login`, `/api/join`
- Internal routes: `/internal/users/*` (exists, nicknames, friends-list visibility)
- Issues JWT via `TokenProvider`; orchestrates user delete across extracted services

### gateway-service

- YARP reverse proxy: routes all `/api/*` and `/hubs/*` to extracted services
- Validates bearer JWT once; forwards `X-User-Id` + `X-Gateway-Secret` to downstream services
- Anonymous: login/join, public user GETs, public media content, public community reads, health

### Runtime (local Compose)

```text
Browser → nginx:8080
  └─ /api/*, /hubs/*, /internal/* → gateway:8080
       ├─ /api/login, /api/join, /api/users/* → users:8080
       ├─ /api/media/* → media:8080
       ├─ … (chat, location, community, group, social)
       └─ JWT validated at gateway; services trust X-User-Id

extracted services ──IUserClient──► users (/internal/users/*)
users ──I*Client──► community, media, chat, group, social, location (user delete detach)
```

### Completed on develop

| Item | Status |
|------|--------|
| `services/Users/` | Done |
| `users` schema + migrations | Done |
| `services/Gateway/` (YARP + JWT middleware) | Done |
| Nginx → single gateway upstream | Done |
| `IUserClient` replaces `IMonolithAccessClient` | Done |
| Gateway identity auth in services | Done |
| `Users.Tests` migration from Api.Tests | Done |
| Monolith removed from default compose / solution | Done (`api` profile `legacy-monolith`) |
| Local Prometheus/Grafana scrape targets | Done (users, media, chat, location, community, group, social) |
| Media harness through gateway | Done (`run-media-harness.sh` starts gateway + users) |

### Still open (before `main` cutover)

| Item | Notes |
|------|-------|
| Azure users + gateway Container Apps | CD + Bicep/parameters |
| `nginx.production.conf` gateway upstream | Strangler for prod |
| Azure domain Container Apps (media, chat, location, community, group, social) | CD + Bicep/parameters per service |

Azure/nginx.production cutover deferred. See [Strangler edge](#strangler-edge) below.

---

## Strangler edge

| Environment | Edge | Routing |
|-------------|------|---------|
| **Compose (`develop`)** | `infra/nginx/nginx.conf` | All `/api/*`, `/hubs/*`, `/internal/*` → `gateway:8080`; gateway YARP routes to users, media, chat, location, community, group, social |
| **Azure (`main`)** | `infra/nginx/nginx.production.conf` | Still monolith-only until cutover |

Gateway validates JWT once; services receive identity via `X-User-Id` (trusted gateway secret). Login/JWT issuance stays on users-service.
