# Service boundaries

Future microservice boundaries align with existing `services/Api/Domain/` folders. Each row below is one target deployable unless noted.

Overview: [ARCHITECTURE.md](ARCHITECTURE.md). Migration order: [MSA_MIGRATION.md](MSA_MIGRATION.md).

---

## Domain → service map

| Service | Source folder | Owned aggregates | API routes (today) | Status |
|---------|---------------|------------------|-------------------|--------|
| **users** | `Domain/Users/` | `User`, JWT auth | `api/users`, `api` (login) | Implemented |
| **posts** | `Domain/Posts/` | `Post` | `api/posts` | Implemented |
| **comments** | `Domain/Comments/` | `Comment` | `api/comments` | Implemented |
| **groups** | `Domain/Groups/` | Group, Board, Member, Invitation, Application, Blacklist | `api/groups/*`, `api/groups/{id}/boards/{id}/posts` | Implemented |
| **friendships** | `Domain/Friendships/` | Friendship, FriendRequest | `api/friendships`, `api/friend-requests` | Implemented |
| **user-blocks** | `Domain/UserBlocks/` | UserBlock | `api/users/blocks` | Implemented |
| **chat** | `Domain/Chat/` | ChatRoom, ChatMessage, Participant | `api/chat/*`, `api/groups/{id}/chat-rooms/*`, SignalR `/hubs/chat` | Implemented |
| **media** | `Domain/Media/` | MediaAsset, processing state | `api/media`, internal processed callback | Implemented — [MEDIA.md](../services/Api/Domain/Media/MEDIA.md) |
| **location** | `Domain/Location/` | `MapPin`, `LocationSession` | `api/location/*`, SignalR `/hubs/location` | Implemented — [LOCATION.md](../services/Api/Domain/Location/LOCATION.md) |

---

## Per-service detail

### users-service

**Owns:** user profiles, authentication (login/JWT), nickname cache reads.

**Read by:** every other service for author display names, privacy settings, block checks.

**Notes:**
- `LoginService` stays with this service.
- `NicknameCacheService` may remain a local cache fed by user events or sync GET.

### posts-service

**Owns:** `Post` (title, content, author reference, optional group/board IDs).

**Depends on:**
- **users** — author nickname enrichment, user existence
- **groups** — board context for group-board posts (`GroupId`, `GroupBoardId` on Post)

**Cross-route today:** group-board posts are created via `GroupBoardPostController` under Groups routes but the aggregate is `Post` ([AGENTS.md](../services/Api/AGENTS.md)). **Phase 9 default:** BFF / gateway compose — gateway calls groups for access check, then posts for CRUD. Full contract: [GROUPS.md](../services/Api/Domain/Groups/GROUPS.md).

### comments-service

**Owns:** `Comment` (content, `PostId`, nested `ParentId`, detach fields).

**Depends on:**
- **posts** — post existence, group-board visibility context (`PostService.TryGetGroupBoardContextAsync`)
- **users** — author identity

**Coupling today:** `CommentService` → `PostService`; `PostService` → `Lazy<CommentService>` for delete/detach. At split, replace with HTTP calls or `post.deleted` events plus local comment cleanup jobs.

### groups-service

**Owns:** groups, boards, memberships, invitations, applications, blacklist.

**Depends on:**
- **users** — member identity, inviter/invitee
- **posts** — board posts (see [GROUPS.md](../services/Api/Domain/Groups/GROUPS.md))
- **chat** — `PlatformGroup` chat rooms link a `ChatRoom` to a `Group` (see [GROUPS.md](../services/Api/Domain/Groups/GROUPS.md))

**Orchestrators:** `GroupJoinResolutionService`, `GroupJoinService` — keep workflow logic inside this service at extraction; do not scatter across posts/chat.

### friendships-service

**Owns:** `Friendship`, `FriendRequest`.

**Depends on:** **users** — both parties must exist.

**Optional merge:** small surface area; could merge into **users** or a future **social-graph** service. Default plan: **domain-aligned** separate service.

### user-blocks-service

**Owns:** `UserBlock`.

**Depends on:** **users**.

**Optional merge:** same as friendships — separate by default, merge later if operational overhead outweighs boundary clarity.

### chat-service

**Owns:** chat rooms, messages, participants, SignalR hub.

**Depends on:**
- **users** — participants, sender identity
- **groups** — `PlatformGroup` room type ties to a group ID — cross-service contract: [GROUPS.md](../services/Api/Domain/Groups/GROUPS.md)

**Async:** enqueues `chat.message.created` to Redis Streams after persist ([QUEUE.md](../services/Api/Global/Queue/QUEUE.md)).

**Realtime:** SignalR hub stays with this service. Redis backplane for multi-replica scale-out.

Hub contract: [CHAT.md](../services/Api/Domain/Chat/CHAT.md).

### media-service

**Implemented in monolith (Phase 4).** Extract at [MSA step 1](MSA_MIGRATION.md#extraction-order). API reference: [MEDIA.md](../services/Api/Domain/Media/MEDIA.md).

**Will own:** `MediaAsset` (storage key, mime, dimensions, processing status, CDN URLs).

**Will reference (by ID only):** posts, comments, chat messages — e.g. `mediaAssetIds[]` on each aggregate, not embedded blob metadata from another service's database.

**Async flow (target):**

```text
Client upload → media-service (presigned URL or direct) → object storage
              → Stream: media.uploaded → rust-worker (transcode, thumbnails)
              → media-service updates asset status → pub/sub: media.ready
              → posts/comments/chat store mediaAssetId only
```

**Worker handlers:** `media.*` job types documented in [QUEUE.md](../services/Api/Global/Queue/QUEUE.md) as they are added.

### location-service

**Implemented in monolith** (`Domain/Location/`). Extraction target at [MSA step 3](MSA_MIGRATION.md#extraction-order) after Phase 7 E2E proof. See [LOCATION.md](../services/Api/Domain/Location/LOCATION.md).

**Owns:**
- Geo metadata on content (`MapPin`, optional post location) — Postgres
- Live location sessions (`LocationSession` with `groupId`) — Postgres headers + Redis TTL positions
- Safety alerts (stale position monitor, manual SOS) — SignalR `SafetyAlertRaised`

**Depends on:**
- **users** — nicknames, blocks
- **groups** — membership for live sharing and alerts
- **posts** (optional) — location-tagged content for Memory Map

**Realtime:** SignalR `/hubs/location` — `LocationUpdated`, `SafetyAlertRaised`; group-scoped live sharing.

**Async:** `location.cluster` stream → `rust-worker-location` for interim pin clustering (zoom 2–4).

**Web:** React `/map` — MapLibre, pins, clusters, group live overlay, sharing status, SOS.

---

## Cross-cutting dependencies

```mermaid
flowchart TB
  Users[users]
  Posts[posts]
  Comments[comments]
  Groups[groups]
  Friendships[friendships]
  UserBlocks[user-blocks]
  Chat[chat]
  Media[media]
  Location[location]

  Posts --> Users
  Posts --> Groups
  Comments --> Posts
  Comments --> Users
  Groups --> Users
  Groups --> Posts
  Groups --> Chat
  Friendships --> Users
  UserBlocks --> Users
  Chat --> Users
  Chat --> Groups
  Media --> Posts
  Media --> Comments
  Media --> Chat
  Location --> Users
  Location --> Posts
```

---

## MSA-prep rules

Apply these during Phase 7 (location in monolith) so later extraction does not require rewrites. Phase 4 (media) already follows these patterns.

1. **No cross-domain repository access** — already enforced in [AGENTS.md](../services/Api/AGENTS.md). Keep it; never add `_db.OtherAggregate` queries.

2. **Reference by ID** — store `userId`, `postId`, `groupId`, `mediaAssetId` across boundaries. No FK joins to another service's tables after split.

3. **Versioned job and event payloads** — Streams in [QUEUE.md](../services/Api/Global/Queue/QUEUE.md); pub/sub in [EVENTS.md](../services/Api/Global/Events/EVENTS.md). Every payload includes `schemaVersion` (currently `1`).

4. **No shared mutable tables for async work** — workers read jobs from Streams and write results back through the owning service's API or a well-defined storage contract, not ad-hoc shared tables.

5. **Explicit contracts before extraction** — Groups ↔ Posts and Groups ↔ Chat documented in [GROUPS.md](../services/Api/Domain/Groups/GROUPS.md) (BFF compose for board posts).

6. **Gateway owns auth context** — JWT validation at the edge; services receive user identity claims, not raw credentials.

---

## Future consolidation options

Not planned for v1 MSA, but documented for later simplification:

| Merge candidate | Rationale |
|-----------------|-----------|
| friendships + user-blocks → social-graph | Small CRUD surfaces, shared user references |
| friendships + user-blocks → users | Fewer deployables; users service grows |
| posts + comments → community | Tight coupling; single "content" service |

Default remains **domain-aligned** split per folder unless operational cost motivates merge.
