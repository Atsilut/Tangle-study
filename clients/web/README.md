# Tangle Web Client

Phase 6 React web client for the Tangle API. Thin, accessibility-minded UI built feature-by-feature in backend order (auth -> posts -> comments -> friends -> groups -> chat -> media). Component and hook reuse is a first-class concern.

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

---

## Connection model (same-origin via Nginx)

The API has **no CORS** configured. The browser always talks to a same-origin path; an Nginx reverse proxy fronts the API in both dev and prod.

```text
dev:   browser -> Vite dev server (:5173) -proxy-> Nginx (:8080) -> api (:8080)
prod:  browser -> Nginx (:8080, serves dist/ + proxies /api,/hubs) -> api (:8080)
```

- Nginx config lives in [../../infra/nginx/nginx.conf](../../infra/nginx/nginx.conf) (infra, not here).
- Vite proxies `/api`, `/hubs` (WebSocket), and `/health` to `VITE_PROXY_TARGET` (default `http://localhost:8080`).
- REST calls go through `/api`; SignalR connects to `/hubs/chat?access_token=<JWT>`.

---

## Running

### Dev (hot reload)

```bash
# 1. Backend + Nginx edge (from repo root)
docker compose --profile web up api db redis nginx

# 2. Web dev server (this folder)
npm install
cp .env.example .env   # adjust VITE_PROXY_TARGET if needed
npm run dev            # http://localhost:5173
```

### Production-style (SPA served by Nginx)

```bash
npm run build                          # emits dist/
docker compose --profile web up --build
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

> Node is not installed in the Docker-first backend workflow; run the web scripts on the host (or via the `web` Docker image build for prod).

---

## Folder structure

```
src/
  lib/           axios client, query client, cn() helper, (signalr later)
  stores/        Zustand stores (authStore)
  types/         shared backend enums / DTO base types
  components/
    ui/          recyclable primitives (Button, Input, Modal, ...)
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
4. **Empty / error / loading** always use `EmptyState`, `ErrorState`, `Spinner`.
5. **List endpoints** that return HTTP 204 are normalized to `[]` by `getList()` in [src/lib/apiClient.ts](src/lib/apiClient.ts).
6. **Auth**: the axios request interceptor attaches `Authorization: Bearer`; a 401 clears the session and redirects to `/login`. There is no `/api/users/me` and no refresh token — `userId` is decoded from the JWT `sub` claim, and an expired token (~15 min) sends the user back to login.
7. **Accessibility**: semantic elements, `<label>`/`aria-*` wiring (see `FormField`), visible focus states, `role="dialog"` modals.

---

## Feature checklist (backend parity)

| # | Feature | Status |
|---|---------|--------|
| 0 | Scaffold + UI kit + layout | Done |
| 1 | Auth (join / login) | Planned |
| 2 | Users (list / profile / privacy) | Planned |
| 3 | Posts | Planned |
| 4 | Comments (threaded) | Planned |
| 5 | Friends + requests | Planned |
| 6 | User blocks | Planned |
| 7 | Groups | Planned |
| 8 | Group members | Planned |
| 9 | Group invitations | Planned |
| 10 | Group applications | Planned |
| 11 | Group blacklist | Planned |
| 12 | Group boards + board posts | Planned |
| 13 | Chat (REST + SignalR) | Planned |
| 14 | Media upload pipeline | Planned |
