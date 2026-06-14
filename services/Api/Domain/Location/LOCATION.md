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
| `GET` | `/api/location/places/search` | Anonymous | `q`, optional `limit` — Google Places text search |
| `GET` | `/api/location/places/reverse` | Anonymous | `latitude`, `longitude` — Google reverse geocode for pin popups |

\* Visibility follows post and block rules: pins linked to posts the viewer cannot read are omitted; pins from blocked users are hidden for authenticated viewers.

### Google Places configuration

Set on the **API** (not the web client). Do **not** commit the API key.

**Docker Compose** — create a local `docker-compose.override.yml` (gitignored) at the repo root:

```yaml
services:
  api:
    environment:
      Places__Enabled: "true"
      Places__ApiKey: "YOUR_KEY_HERE"
```

Then `docker compose up -d api`.

**Host `dotnet run`** (Development profile) — add to local `services/Api/appsettings.Development.json` (do not commit the key):

```json
"Places": {
  "Enabled": true,
  "ApiKey": "YOUR_KEY_HERE"
}
```

Enable **Places API (New)** and **Geocoding API** on the key. When disabled, search returns `204 No Content` and the map UI shows no suggestions.

Swagger: `http://localhost:5000/api` under `api/location/pins`.

---

## Data model

| Store | Content |
|-------|---------|
| **Postgres** | `MapPin` — durable geo metadata (`latitude`, `longitude`, optional `postId`, owner `userId`) |

Coordinates use `decimal(9,6)` (plain lat/lng, no PostGIS in v1).

Posts may optionally attach a location on create or patch (`latitude` / `longitude` on post DTOs). `PostService` upserts or clears the linked `MapPin` via `MapPinService`; post delete cascades the pin via FK.

---

## Web client (Memory Map)

[clients/web](../../../../clients/web/README.md): MapLibre renders **OpenStreetMap** raster tiles; place search and pin popups use **Google Places** via `/api/location/places` (see configuration above).

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
