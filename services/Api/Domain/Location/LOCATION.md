# Location API

Memory Map pins and live location sharing in the monolith, including group safety alerts.

Related: [SERVICE_BOUNDARIES.md](../../../../docs/SERVICE_BOUNDARIES.md#location-service).

---

## REST (Memory Map pins)

| Method | Route | Auth | Notes |
|--------|-------|------|-------|
| `POST` | `/api/location/pins` | Bearer | Create a pin at `latitude` / `longitude`; optional `postId` (caller must own the post) |
| `GET` | `/api/location/pins` | Anonymous* | Bounding-box query: `minLatitude`, `maxLatitude`, `minLongitude`, `maxLongitude` |
| `GET` | `/api/location/pins/{id}` | Anonymous* | Single pin by id |
| `DELETE` | `/api/location/pins/{id}` | Bearer | Delete own pin |
| `GET` | `/api/location/clusters` | Anonymous* | Bounding box + `zoom` (2–4); returns cached clusters or `204` while worker computes |
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

## Clustering

### Target (content-driven cluster tiles)

The map is a **content discovery** surface — clusters are content cards, not pin counters. Read latency is the top priority; aggregation is **write-time**, not request-time.

```text
Write path:
  Post / MapPin change
      → location.tile.aggregate job
      → update ClusterTile rows (all aggregation levels)
      → invalidate Redis tile cache

Read path:
  GET /api/location/cluster-tiles?bbox&mapZoom
      → DensityResolver (pick aggregation level for 20–60 clusters in viewport)
      → ClusterTile query (Postgres, indexed by z/x/y)
      → Redis cache (optional)
      → enrich preview URLs via MediaService (IDs only stored on tile)
```

**Aggregation levels** (fixed set, each with its own tile dataset):

| Level | Typical scope |
|-------|----------------|
| 5 | World / continent |
| 8 | Country / large region |
| 10 | City |
| 12 | District |
| 14 | Neighborhood → individual posts at high map zoom |

Tile key: Web Mercator `(aggregationZoom, tileX, tileY)` — same scheme as map tiles, easy bbox → tile range math.

**Cluster tile row** (`ClusterTile`):

| Column | Purpose |
|--------|---------|
| `AggregationZoom`, `TileX`, `TileY` | Primary key |
| `PostCount` | Posts in tile |
| `CentroidLatitude`, `CentroidLongitude` | Map marker |
| `CoverMediaAssetId` | Representative thumbnail |
| `PreviewMediaAssetIds` | JSON array, up to 3–4 |
| `LatestPostId`, `LatestPostAt` | Recency |
| `RegionName` | Precomputed label (reverse geocode at write time or admin region) |

**Read response** (example):

```json
{
  "clusterId": "10/532/381",
  "aggregationZoom": 10,
  "postCount": 182,
  "latitude": 37.4979,
  "longitude": 127.0276,
  "previewImageUrls": ["...", "...", "..."],
  "coverImageUrl": "...",
  "latestPostAt": "2026-06-14T07:00:00Z",
  "regionName": "Gangnam"
}
```

**Density resolver** (read path, no real-time clustering):

1. Input: viewport bbox + user `mapZoom`.
2. For each aggregation level, count **precomputed tiles** intersecting the bbox (indexed lookup, not pin scan).
3. Select the level whose tile count is closest to the **20–60** target range.
4. If the finest level still yields &lt; 20 tiles and `mapZoom` is high enough, fall through to individual post markers (`GET /api/location/pins` or a dedicated posts-in-bbox endpoint).

The aggregation level returned to the client may differ from `mapZoom` — that is intentional (content discovery over strict zoom parity).

**Visibility:** v1 tiles aggregate **public** content only (same rules as anonymous cluster-points today). Per-viewer filtering at millions of posts requires either viewer-scoped tile sets (expensive) or post-query filtering on a bounded result set — defer until scale demands it.

**Scale path:** Postgres `ClusterTile` → Redis hot tiles → Vector Tile API (MVT) when post counts reach millions.

### Interim (current implementation)

Until cluster tiles land, the map uses a simplified path:

- Zoom 2–4: `GET /api/location/clusters` (pin count + centroid only).
- Zoom 5+: individual pins via `GET /api/location/pins`.
- On cache miss: `location.cluster` job → `rust-worker-location` → Redis (5 min TTL).

This interim path lacks content metadata, density resolution, and precomputed tiles. **Do not extend it** — replace with cluster tiles above.

Worker: `docker compose --profile workers up -d rust-worker-location`.

---

## Live location sharing

Authenticated **group members** can share live position within a group:

| Method | Route | Auth | Notes |
|--------|-------|------|-------|
| `POST` | `/api/location/sessions` | Bearer | Start sharing; body includes `groupId`, `latitude`, `longitude` |
| `GET` | `/api/location/sessions/mine?groupId=` | Bearer | Active session in that group or `204` |
| `GET` | `/api/location/sessions/active?groupId=` | Bearer | Other members' live locations in that group or `204` |
| `GET` | `/api/location/sessions/members?groupId=` | Bearer | Sharing status for other group members (`isSharing` true/false) |
| `PATCH` | `/api/location/sessions/{id}/position` | Bearer | Update position; pushes `LocationUpdated` via SignalR |
| `DELETE` | `/api/location/sessions/{id}` | Bearer | Stop sharing |
| `POST` | `/api/location/sessions/{id}/sos` | Bearer | Manual SOS alert to group members |

SignalR hub: `/hubs/location`

| Client method | Description |
|---------------|-------------|
| `JoinSession(sessionId)` / `LeaveSession(sessionId)` | Live position updates (`LocationUpdated`) |
| `JoinGroupAlerts(groupId)` / `LeaveGroupAlerts(groupId)` | Safety alerts for a group (`SafetyAlertRaised`) |

Live positions are cached in Redis (`location:live:{groupId}:{userId}`) with a five-minute TTL, refreshed on each position update. Session headers live in Postgres (`LocationSession` with `groupId`).

---

## Safety alerts

Group members subscribed via `JoinGroupAlerts` receive `SafetyAlertRaised` events.

| Alert type | Trigger |
|------------|---------|
| `StalePosition` | Background monitor: active session with no position update for `LocationSafety:StalePositionMinutes` (default **3**) |
| `Sos` | `POST /api/location/sessions/{id}/sos` while sharing |

Stale alerts dedupe per session until the next position update. Monitor interval: `LocationSafety:MonitorIntervalSeconds` (default **60**).

---

## Planned (later milestones)

| Feature | Mechanism |
|---------|-----------|
| _(none — Phase 7 location milestones complete in monolith)_ |

Job and event contracts will be documented in [Global/Queue/QUEUE.md](../../Global/Queue/QUEUE.md) as they are added.

---

## MSA-prep

- Reference `userId` and `postId` by ID only across services.
- No cross-domain repository access — `MapPinService` uses `PostService` and `UserService`, not their repositories.
- Extraction target: `location-service` (Phase 9); do not greenfield a separate deployable before monolith E2E proof.
