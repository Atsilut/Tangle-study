# Chat API

REST owns chat persistence and permissions; SignalR pushes new messages to clients that have joined a room broadcast group.

## REST (summary)

| Method | Route | Notes |
|--------|-------|--------|
| `POST` | `/api/chat/rooms/direct` | Get or create 1:1 (friends, no blocks) |
| `POST` | `/api/chat/rooms/multi` | Ad-hoc group; all members equal |
| `POST` | `/api/groups/{groupId}/chat-rooms` | Platform-group room; creator is owner |
| `GET` | `/api/chat/rooms` | Rooms you participate in |
| `GET` | `/api/groups/{groupId}/chat-rooms` | All chat rooms under a group (members only) |
| `GET` | `/api/chat/rooms/{roomId}` | Room detail (participants only) |
| `POST` | `/api/chat/rooms/{roomId}/participants` | Direct/multi: any participant (direct → promotes to multi); platform-group: owner only |
| `DELETE` | `/api/chat/rooms/{roomId}/participants/me` | Leave room |
| `GET` | `/api/chat/rooms/{roomId}/messages` | `?before={messageId}&limit=` (default 50, max 100) |
| `POST` | `/api/chat/rooms/{roomId}/messages/seen` | Mark message IDs as read (others' messages only) |
| `POST` | `/api/chat/rooms/{roomId}/messages` | Send message (participants only) |
| `PATCH` | `/api/chat/rooms/{roomId}/messages/{messageId}` | Edit body (sender, policy window) |
| `DELETE` | `/api/chat/rooms/{roomId}/messages/{messageId}` | Soft-delete (sender, policy window, unseen by others) |

Room kinds: `Direct`, `Multi`, `PlatformGroup`. See Swagger under `api/chat` and `api/groups/{groupId}/chat-rooms`.

### Internal routes (`X-Internal-Secret`)

| Method | Route | Caller | Notes |
|--------|-------|--------|-------|
| `POST` | `/internal/chat/users/{userId}/detach-on-deletion` | Monolith | User deletion cleanup |
| `POST` | `/internal/chat/messages/{chatMessageId}/media-view` | Media-service | Chat attachment ACL check (also requires caller JWT) |

Shared secret: `InternalAccess:Secret` on chat-service; `Monolith:InternalSecret` / `ChatClient:InternalSecret` on callers. See [SERVICE_BOUNDARIES.md](../../docs/SERVICE_BOUNDARIES.md#internal-service-authentication).

---

## Realtime (SignalR)

## Hub

| Item | Value |
|------|--------|
| URL | `/hubs/chat` |
| Auth | JWT (same as REST) |

### JWT on WebSocket negotiate

Browsers cannot set `Authorization` on the WebSocket handshake. Pass the access token as a query parameter:

```
/hubs/chat?access_token=<JWT>
```

The API copies `access_token` into the bearer handler for paths under `/hubs`.

## Client → server methods

| Method | Parameters | Description |
|--------|------------|-------------|
| `JoinRoom` | `roomId` (long) | Verifies membership, adds connection to SignalR group `room:{roomId}` (not the platform `Group` entity) |
| `LeaveRoom` | `roomId` (long) | Removes connection from that broadcast group |

Call `JoinRoom` after connecting and whenever the user opens a chat screen. Call `LeaveRoom` when leaving the screen (optional but recommended).

## Server → client events

| Event | Payload | When |
|-------|---------|------|
| `MessageCreated` | `ChatMessageGetResponseDto` | After `POST /api/chat/rooms/{roomId}/messages` succeeds |
| `MessageEdited` | `ChatMessageGetResponseDto` | After `PATCH /api/chat/rooms/{roomId}/messages/{messageId}` succeeds |
| `MessageDeleted` | `ChatMessageGetResponseDto` | After `DELETE /api/chat/rooms/{roomId}/messages/{messageId}` succeeds |

Payload shape (JSON):

```json
{
  "id": 1,
  "chatRoomId": 1,
  "senderUserId": 2,
  "senderNickname": "alice",
  "body": "Hello",
  "sentAt": "2026-05-27T12:00:00Z",
  "updatedAt": "2026-05-27T12:00:00Z",
  "isDeleted": false,
  "isEdited": false,
  "canEdit": true,
  "canDelete": true,
  "editHistory": null,
  "media": null
}
```

Edit/delete policy (`Chat` in `appsettings.json`): default **15** minutes to edit, **60** minutes to delete. Delete is blocked once another participant has marked the message seen (read receipts); edit is allowed within the time window even after seen. The client calls `POST .../messages/seen` when others' messages are displayed — listing messages alone does not create receipts.

## Recommended client flow

1. Log in via `POST /api/login` and store `accessToken`.
2. **Load history** via `GET /api/chat/rooms/{roomId}/messages?before=&limit=50` (Postgres; required for context if the user was offline or joins late).
3. **Mark seen** via `POST /api/chat/rooms/{roomId}/messages/seen` with `{ "messageIds": [...] }` for others' messages shown in the UI.
4. Connect to `/hubs/chat?access_token=...`.
5. `JoinRoom(roomId)` for each open conversation (subscribes to live pushes only; does not replay past messages).
6. Listen for `MessageCreated`, `MessageEdited`, and `MessageDeleted`; mark incoming others' messages seen when displayed.
7. Send text via `POST /api/chat/rooms/{roomId}/messages` (do not send chat text only over the hub).

Planned: return the latest messages from `JoinRoom` to reduce round-trips (not implemented yet).

## Scaling and Redis

| Mode | Behavior |
|------|----------|
| `Redis:Enabled=false` | In-process SignalR groups; fine for single API instance and tests |
| `Redis:Enabled=true` | SignalR Redis backplane syncs groups across API replicas |

See [Api REDIS.md](../Api/Global/REDIS.md) for cache, pub/sub, and Streams. **Postgres remains message history; Redis does not store chat transcripts for clients.**
