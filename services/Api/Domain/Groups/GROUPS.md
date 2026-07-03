# Groups API and cross-service contracts

Groups bounded context: memberships, boards, invitations, applications, blacklist. Extraction target: **groups-service** ([MSA step 5](../../../docs/MSA_MIGRATION.md#extraction-order)).

This doc records **cross-route contracts** needed before Phase 9 split. Today everything runs in the monolith; routes and DTOs below are the source of truth for future HTTP boundaries.

Related: [SERVICE_BOUNDARIES.md](../../../docs/SERVICE_BOUNDARIES.md), [AGENTS.md](../../AGENTS.md).

---

## REST (summary)

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/groups` | List / discover groups |
| `POST` | `/api/groups` | Create group |
| `GET` | `/api/groups/{groupId}` | Group detail (limited profile for non-members on private groups) |
| `PATCH` | `/api/groups/{groupId}` | Update group (owner/admin) |
| `DELETE` | `/api/groups/{groupId}` | Delete group |
| `GET` | `/api/groups/{groupId}/members` | List members |
| `POST` | `/api/groups/{groupId}/members` | Add member (policy-dependent) |
| `GET` | `/api/groups/{groupId}/boards` | List boards |
| `POST` | `/api/groups/{groupId}/boards` | Create board |
| `GET` | `/api/groups/{groupId}/boards/{boardId}` | Board detail |
| `PATCH` | `/api/groups/{groupId}/boards/{boardId}` | Update board |
| `DELETE` | `/api/groups/{groupId}/boards/{boardId}` | Delete board |

Board posts and group chat rooms are **cross-route** — see sections below.

---

## Groups ↔ Posts (board posts)

### Today (monolith)

Public routes live under Groups; aggregate is `Post`:

| Method | Route | Handler |
|--------|-------|---------|
| `POST` | `/api/groups/{groupId}/boards/{boardId}/posts` | `GroupBoardPostController` → `PostService.CreateGroupBoardPostAsync` |
| `GET` | `/api/groups/{groupId}/boards/{boardId}/posts` | `PostService.GetGroupBoardPostsAsync` |
| `GET` | `/api/groups/{groupId}/boards/{boardId}/posts/{postId}` | `PostService.GetGroupBoardPostByIdAsync` |

Access checks run in `GroupBoardAccessService` before post CRUD:

- **Write:** `EnsureCanWritePostAsync` — board writeability (`ForAll`, `MembersOnly`, `AdminOnly`) + group/board visibility rules.
- **Read:** `EnsureCanViewBoardAsync` — public boards on public groups, or member/admin visibility per board settings.

Request body (`GroupBoardPostCreateRequestDto`):

| Field | Type | Required |
|-------|------|----------|
| `title` | `string` (1–100) | yes |
| `content` | `string` (1–100) | yes |
| `mediaAssetIds` | `long[]?` | no |
| `latitude` / `longitude` | `decimal?` | optional pair |

Response (`PostGetResponseDto`): `id`, `title`, `content`, timestamps, `authorId`, `authorNickname`, `media`, optional `location`.

### Phase 9 target: BFF / gateway compose

**Chosen pattern:** gateway receives the public route, orchestrates two services — **not** groups-service proxying to posts.

```text
Client → Gateway
           ├─ POST .../groups/{groupId}/boards/{boardId}/posts
           │     1. groups-service: validate write access (internal)
           │     2. posts-service: create post with IDs + body
           └─ GET ... (same split: groups validates view, posts returns list/detail)
```

#### Internal: groups-service (access check)

Gateway forwards JWT `sub` as `X-User-Id` (or equivalent claim). Suggested internal endpoints:

| Method | Route | Purpose |
|--------|-------|---------|
| `POST` | `/internal/groups/{groupId}/boards/{boardId}/access/write` | Returns `204` if caller may write; else `401` / `404` |
| `POST` | `/internal/groups/{groupId}/boards/{boardId}/access/read` | Returns `204` if caller may read; else `401` / `404` |

Semantics match today's `GroupBoardAccessService` (same error codes as monolith).

#### Internal: posts-service (CRUD)

After groups approves access, gateway calls posts-service:

| Method | Route | Body |
|--------|-------|------|
| `POST` | `/internal/posts/group-board` | `{ groupId, groupBoardId, authorUserId, title, content, mediaAssetIds?, latitude?, longitude? }` |
| `GET` | `/internal/posts/group-board?groupId=&boardId=` | List (gateway already validated read) |
| `GET` | `/internal/posts/group-board/{postId}?groupId=&boardId=` | Single post |

`Post` stores `GroupId` and `GroupBoardId` **by ID only**. At extraction, drop EF FKs from `Post` to `Group` / `GroupBoard` — retain columns, remove navigations.

#### Error codes (public route)

| Condition | HTTP |
|-----------|------|
| Not authenticated | **401** |
| Board not found | **404** |
| Authenticated but cannot view/write | **401** |
| Invalid body (title/content/location pair) | **400** |
| Author user missing | **400** |

---

## Groups ↔ Chat (`PlatformGroup` rooms)

### Today (monolith)

| Method | Route | Handler |
|--------|-------|---------|
| `GET` | `/api/groups/{groupId}/chat-rooms` | `GroupChatRoomController` → `ChatRoomService.GetPlatformGroupRoomsAsync` |
| `POST` | `/api/groups/{groupId}/chat-rooms` | `ChatRoomService.CreatePlatformGroupRoomAsync` |

Create request (`ChatRoomPlatformGroupCreateRequestDto`):

| Field | Type | Required |
|-------|------|----------|
| `title` | `string?` (max 200) | no |
| `participantUserIds` | `long[]` (min 1) | yes — initial participants besides creator |

Creator is added automatically as **room owner**. All listed participants must be **members of the platform group**.

Access (`ChatRoomAccessService`):

- **List:** `EnsureGroupMemberCanListRoomsAsync` — group exists + caller is member.
- **Create:** `EnsureCanCreatePlatformGroupRoomAsync` — creator is member; other participants exist, no blocks, all are group members.
- **Add participant (existing room):** `EnsureInviteeCanBeAddedAsync` — for `PlatformGroup` rooms, invitee must be a group member.

`ChatRoom` stores `PlatformGroupId` by ID. At extraction, drop EF FK from `ChatRoom` to `Group`; keep `PlatformGroupId` column and check constraint on kind.

### Phase 9 target: split validation

```text
Client → Gateway
           POST /api/groups/{groupId}/chat-rooms
             1. groups-service: validate group + membership + participant membership
             2. chat-service: create PlatformGroup room with platformGroupId = groupId
```

#### Internal: groups-service

| Method | Route | Purpose |
|--------|-------|---------|
| `POST` | `/internal/groups/{groupId}/members/validate` | Body: `{ userIds[] }` — all must be members; `400` if not |
| `GET` | `/internal/groups/{groupId}/membership/me` | Caller is member → `204`; else `401` / `404` |

#### Internal: chat-service

| Method | Route | Body |
|--------|-------|------|
| `POST` | `/internal/chat/rooms/platform-group` | `{ platformGroupId, creatorUserId, title?, participantUserIds[] }` |
| `GET` | `/internal/chat/rooms/platform-group/{platformGroupId}` | List rooms (gateway validates list access via groups first) |

Chat-service **does not** join to groups tables — only stores `platformGroupId`. Group membership rules stay in groups-service.

#### PlatformGroup room rules (unchanged at split)

| Rule | Behavior |
|------|----------|
| Room owner | Creator on create; required for `POST .../participants` on platform-group rooms |
| Multiple rooms per group | Allowed |
| Participant add | Owner only; invitee must be group member |
| SignalR | `/hubs/chat` — see [CHAT.md](../../../Chat/CHAT.md) |

---

## MSA-prep (Phase 8)

- Cross-domain access uses **services only** — `GroupBoardAccessService` + `PostService`, `ChatRoomAccessService` + `GroupMembershipService`.
- Board post DTOs live under `Domain/Posts/Dto/`; route ownership stays with Groups controllers ([AGENTS.md](../../AGENTS.md)).
- Async contracts: [QUEUE.md](../../Global/Queue/QUEUE.md), [EVENTS.md](../../Global/Events/EVENTS.md).

Extraction order: posts/comments and chat leave the monolith **before** groups ([MSA_MIGRATION.md](../../../docs/MSA_MIGRATION.md)); this doc defines the contracts groups will need when orchestrated from the gateway.
