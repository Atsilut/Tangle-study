# Tangle Web Client

React web client for the Tangle API. Thin, accessibility-minded UI covering auth, posts, comments, friends, blocks, groups, chat, and media. Component and hook reuse is a first-class concern.

Project overview: [../../README.md](../../README.md). Backend feature reference: Swagger at `http://localhost:5000/api`.

---

## Stack

| Concern | Choice |
|---------|--------|
| Build / dev server | Vite 8 |
| UI | React 19 + TypeScript |
| Styling | Tailwind CSS 4 (`@tailwindcss/vite`) |
| Server state | TanStack Query 5 |
| Client/global state | Zustand 5 (persisted auth) |
| Routing | react-router 7 |
| HTTP | axios (interceptors for auth + 204/401) |
| Realtime | `@microsoft/signalr` (chat) |
| Maps | MapLibre GL + OpenStreetMap tiles + Google Places (via API) |

---

## Connection model (same-origin via Nginx)

The API has **no CORS** configured. The browser always talks to a same-origin path; an Nginx reverse proxy fronts the API in both dev and prod.

```text
dev:   browser -> Vite dev server (:5173) -proxy-> Nginx (:8080) -> api (:8080)
prod:  browser -> Nginx (:8080, serves dist/ + proxies /api,/hubs) -> api (:8080)
```

- Nginx config lives in [../../infra/nginx/nginx.conf](../../infra/nginx/nginx.conf) (infra, not here).
- Vite proxies `/api`, `/hubs` (WebSocket), `/health`, and `/devstoreaccount1` (local Azurite blob uploads) to the backend edge / storage.
- REST calls go through `/api`; SignalR connects to `/hubs/chat?access_token=<JWT>`.
- Memory Map uses OpenStreetMap tiles (MapLibre) and Google Places search via `/api/location/places` (API key configured on **location-service** — see [LOCATION.md](../../services/Location/LOCATION.md)).

---

## Running

### Dev (hot reload)

```bash
# 1. Backend + Nginx edge (from repo root)
docker compose up api db redis azurite nginx

# 2. Web dev server (this folder)
npm install
cp .env.example .env   # adjust VITE_PROXY_TARGET if needed
npm run dev            # http://localhost:5173
```

### Production-style (SPA served by Nginx)

```bash
# From repo root — SPA is built into the nginx image
docker compose up --build
# browse http://localhost:8080
```

### Scripts

| Script | Purpose |
|--------|---------|
| `npm run dev` | Vite dev server |
| `npm run build` | `tsc --noEmit` typecheck + production build to `dist/` |
| `npm run preview` | Preview the production build |
| `npm run lint` | ESLint (flat config, zero-warning policy) |
| `npm run format` | Prettier |
| `npm run test` | Vitest (unit tests for helpers/stores) |
| `npm run test:watch` | Vitest watch mode |

> Node is not required for the Docker-first workflow: `docker compose up --build` builds and serves the SPA in the `nginx` image. Run the npm scripts on the host only for Vite dev (hot reload) or local lint/test. From repo root, `./scripts/run-all-tests.sh` runs web tests in Docker along with API, Rust, and harness suites — see [README.md](../../README.md#tests-testcontainers-needs-docker-socket).

### Reset dev data (smoke tests)

Wipe test users and other rows from the local Compose Postgres (schema and migrations are kept):

```bash
# from repo root
./scripts/dev-clear-db.sh          # prompts for confirmation
./scripts/dev-clear-db.sh --yes    # non-interactive
```

Requires the Compose `db` service to be running (`docker compose up -d db` or full stack). Does not clear Redis or Azurite. After reset, clear `tangle-auth` in browser local storage (or sign out) before signing up again.

See [../../README.md](../../README.md#reset-local-dev-data) for details.

---

## Folder structure

```
src/
  lib/           axios client, query client, cn() helper
  stores/        Zustand stores (authStore)
  types/         shared backend enums / DTO base types
  components/
    ui/          recyclable primitives (Button, Input, Modal, ...)
    common/      QueryBoundary, CenteredSpinner, UserRow, ...
    layout/      AppShell, Navbar, Sidebar, ProtectedRoute
  features/      per-feature slices: api.ts, hooks.ts, components/, pages/
  pages/         top-level route pages not owned by a feature
  routes.tsx     route table (AppShell layout route + feature routes)
  main.tsx       providers (QueryClient, Router)
```

---

## Conventions (recycling-first)

1. **Reuse primitives.** Compose pages from `components/ui` — never hand-roll a button/input/modal per feature. Import from the barrel: `import { Button, Card } from '@/components/ui'`.
2. **One folder per feature** under `features/`, each with:
   - `api.ts` — typed request functions over the shared axios client.
   - `hooks.ts` — TanStack Query query/mutation hooks + a query-key factory.
   - `components/`, `pages/` — feature UI built on the kit.
3. **Query keys** come from a per-feature factory; mutations invalidate via those keys.
4. **Empty / error / loading** use `EmptyState`, `ErrorState`, `Spinner`, and `CenteredSpinner` (via `QueryBoundary` or role gates).
5. **List endpoints** that return HTTP 204 are normalized to `[]` by `getList()` in [src/lib/apiClient.ts](src/lib/apiClient.ts).
6. **Auth**: the axios request interceptor attaches `Authorization: Bearer`; a 401 clears the session and redirects to `/login`. There is no `/api/users/me` and no refresh token — `userId` is decoded from the JWT `sub` claim, and an expired token (~15 min) sends the user back to login.
7. **Accessibility**: semantic elements, `<label>`/`aria-*` wiring (see `FormField`), visible focus states, `role="dialog"` modals.
8. **Memory Map** (`/map`): OpenStreetMap raster tiles via MapLibre; place search and reverse geocoding via `/api/location/places` (enable `Places:Enabled` + `Places:ApiKey` on **location-service** — see [LOCATION.md](../../services/Location/LOCATION.md)). MapLibre worker: `lib/maplibreSetup.ts`.
   - **Pins** — double-click to drop; bbox fetch at zoom 5+.
   - **Clusters** — zoom 2–4 (needs `rust-worker-location` with `--profile workers`).
   - **Live sharing** — select a group, start/stop sharing; green markers for other members; member list shows sharing vs not sharing.
   - **Safety** — SOS button while sharing; stale-position and SOS alerts via SignalR (`features/location/signalr.ts`).
   - **You** — pulsing magenta marker from browser geolocation (`useMyGeolocation`).
