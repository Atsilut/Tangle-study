# Location API

Memory Map pins and live location sharing in **location-service**, including group safety alerts.

Related: [SERVICE_BOUNDARIES.md](../../../../docs/SERVICE_BOUNDARIES.md#location-service).

---

## REST (Memory Map pins)

| Method | Route | Auth | Notes |
|--------|-------|------|-------|
| `POST` | `/api/location/pins` | Bearer | Create a pin at `latitude` / `longitude`; optional `postId` (caller must own the post) |
| `GET` | `/api/location/pins` | Bearer | Bounding-box query: `minLatitude`, `maxLatitude`, `minLongitude`, `maxLongitude` |
| `GET` | `/api/location/pins/{id}` | Bearer | Single pin by id |
| `DELETE` | `/api/location/pins/{id}` | Bearer | Delete own pin |
| `GET` | `/api/location/clusters` | Bearer | Bounding box + `zoom` (2–4); rate-limited; returns cached clusters or `204` while worker computes |
| `GET` | `/api/location/places/search` | Bearer | `q`, optional `limit` — Google Places text search |
| `GET` | `/api/location/places/reverse` | Bearer | `latitude`, `longitude` — Google reverse geocode for pin popups |

\* Visibility: standalone pins (no `postId`) are visible only to the owner. Post-linked pins follow board/post visibility. Blocked users' pins are hidden. Cluster reads require sign-in and share the `location-clusters` rate limit (default 120/min per IP).

### Google Places configuration

Set on **location-service** (not the web client). Do **not** commit the API key.

**Docker Compose** — create a local `docker-compose.override.yml` (gitignored) at the repo root:

```yaml
services:
  location:
    environment:
      Places__Enabled: "true"
      Places__ApiKey: "YOUR_KEY_HERE"
```

Then `docker compose up -d location`.

**Local service run** (Development/Docker profile) — override via environment or `appsettings.Docker.json` (do not commit the key):

```json
"Places": {
  "Enabled": true,
  "ApiKey": "YOUR_KEY_HERE"
}
```

Enable **Places API (New)** and **Geocoding API** on the key. When disabled, search returns `204 No Content` and the map UI shows no suggestions.

Swagger: `http://localhost:8080/api` under `api/location/pins` (via nginx) or the location service port directly in dev.

---

## Data model

| Store | Content |
|-------|---------|
| **Postgres (`location` schema)** | `MapPin` — durable geo metadata (`latitude`, `longitude`, optional `postId`, owner `userId`); `LocationSession` — live sharing session headers |

Coordinates use `decimal(9,6)` (plain lat/lng, no PostGIS in v1).

Posts may optionally attach a location on create or patch (`latitude` / `longitude` on post DTOs). Monolith `PostService` upserts or clears the linked pin via `ILocationClient`; post delete and group delete call location-service explicitly (no cross-schema FKs).

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
- On cache miss: `204` + `location.cluster` job → `rust-worker-location` → Redis (5 min TTL); empty results are cached as `200 []` so clients stop polling.
- Worker clusters **all pins in bounds** (shared cache is not per-viewer) using a ~50px grid cell so nearby pins merge into one marker with a pin count. Pin list endpoints still apply owner/post visibility.

This interim path lacks content metadata, density resolution, and precomputed tiles. **Do not extend it** — replace with cluster tiles above.

Worker: `docker compose --profile workers up -d rust-worker-location`.

Internal worker routes (`GET /internal/location/cluster-points`, `PUT /internal/location/clusters`) use the same `X-Worker-Callback-Secret` header as media workers — configured via `WorkerCallback:Secret` on location-service (see [MEDIA.md](../Media/MEDIA.md) for media worker callbacks).

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

| Server event | Payload | When |
|--------------|---------|------|
| `LocationUpdated` | `LiveLocationGetResponseDto` | After position update while sharing |
| `LocationSessionEnded` | `LocationSessionEndedDto` | Session stopped, ghost reconciled, or user leaves group |
| `SafetyAlertRaised` | `LocationSafetyAlertDto` | Stale position or SOS |

Live positions are cached in Redis (`location:live:{groupId}:{userId}`) with TTL from `LocationSafety:LivePositionTtlMinutes` in [`location-config.yml`](location-config.yml), refreshed on each position update. Session headers live in Postgres (`LocationSession` with `groupId`).

---

## Safety alerts

Group members subscribed via `JoinGroupAlerts` receive `SafetyAlertRaised` events.

| Alert type | Trigger |
|------------|---------|
| `StalePosition` | Background monitor: active session with no position update for `LocationSafety:StalePositionMinutes` (see `location-config.yml`) |
| `Sos` | `POST /api/location/sessions/{id}/sos` while sharing |

Stale alerts dedupe per session until the next position update. Monitor interval: `LocationSafety:MonitorIntervalSeconds` (see `location-config.yml`).

Async job contract for map clustering: [QUEUE.md](../Api/Global/Queue/QUEUE.md) (`location.cluster`).

---

## Configuration (`location-config.yml`)

Baked into the image; override via env (`Places__ApiKey`, `Redis__*`, etc.) when needed. Startup fails if required keys are missing.

| Section | Keys | Notes |
|---------|------|-------|
| `LocationSafety` | `StalePositionMinutes`, `LivePositionTtlMinutes`, `MonitorIntervalSeconds`, `SosCooldownSeconds` | `LivePositionTtlMinutes` must be greater than `StalePositionMinutes` |
| `LocationCluster` | `RateLimitPerMinute` | Public cluster read rate limit |
| `Places` | `Enabled`, `RateLimitPerMinute` | `ApiKey` via env / secrets, not committed |
| `Redis` | `InstanceName`, `WorkQueueStreamPrefix`, `SignalRChannelPrefix` | Prefixes shared with other services |

---

## Monolith integration

The monolith and community call location-service over HTTP (`ILocationClient`) for post pin upsert/clear, user detach on deletion, and group session end. Location-service calls the monolith via `IMonolithAccessClient` for users and blocks, group-service via `IGroupClient` for membership, and community via `ICommunityAccessClient` for post visibility. See [SERVICE_BOUNDARIES.md](../../docs/SERVICE_BOUNDARIES.md#location-service).
