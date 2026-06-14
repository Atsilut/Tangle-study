# Location API

Memory Map pins in the monolith. Live location sessions, SignalR, and worker clustering land in later milestones.

Related: [SERVICE_BOUNDARIES.md](../../../../docs/SERVICE_BOUNDARIES.md#location-service).

---

## REST (Memory Map pins)

| Method | Route | Auth | Notes |
|--------|-------|------|-------|
| `POST` | `/api/location/pins` | Bearer | Create a pin at `latitude` / `longitude`; optional `postId` (caller must own the post) |
| `GET` | `/api/location/pins` | Anonymous* | Bounding-box query: `minLatitude`, `maxLatitude`, `minLongitude`, `maxLongitude` |
| `GET` | `/api/location/pins/{id}` | Anonymous* | Single pin by id |
| `DELETE` | `/api/location/pins/{id}` | Bearer | Delete own pin |

\* Visibility follows post and block rules: pins linked to posts the viewer cannot read are omitted; pins from blocked users are hidden for authenticated viewers.

Swagger: `http://localhost:5000/api` under `api/location/pins`.

---

## Data model

| Store | Content |
|-------|---------|
| **Postgres** | `MapPin` — durable geo metadata (`latitude`, `longitude`, optional `postId`, owner `userId`) |

Coordinates use `decimal(9,6)` (plain lat/lng, no PostGIS in v1).

---

## Planned (later milestones)

| Feature | Mechanism |
|---------|-----------|
| Live location sharing | Redis TTL store + REST session endpoints |
| Client push | SignalR hub at `/hubs/location` |
| Memory Map clustering | Redis Streams job `location.cluster` + rust-worker handler |
| Safety alerts | SignalR events |

Job and event contracts will be documented in [Global/Queue/QUEUE.md](../../Global/Queue/QUEUE.md) as they are added.

---

## MSA-prep

- Reference `userId` and `postId` by ID only across services.
- No cross-domain repository access — `MapPinService` uses `PostService` and `UserService`, not their repositories.
- Extraction target: `location-service` (Phase 9); do not greenfield a separate deployable before monolith E2E proof.
