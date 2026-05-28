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
| `POST` | `/api/chat/rooms/{roomId}/messages` | Send message (participants only) |

Room kinds: `Direct`, `Multi`, `PlatformGroup`. See Swagger under `api/chat` and `api/groups/{groupId}/chat-rooms`.

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

Payload shape (JSON):

```json
{
  "id": 1,
  "chatRoomId": 1,
  "senderUserId": 2,
  "senderNickname": "alice",
  "body": "Hello",
  "sentAt": "2026-05-27T12:00:00Z"
}
```

## Recommended client flow

1. Log in via `POST /api/login` and store `accessToken`.
2. **Load history** via `GET /api/chat/rooms/{roomId}/messages?before=&limit=50` (Postgres; required for context if the user was offline or joins late).
3. Connect to `/hubs/chat?access_token=...`.
4. `JoinRoom(roomId)` for each open conversation (subscribes to live pushes only; does not replay past messages).
5. Listen for `MessageCreated`.
6. Send text via `POST /api/chat/rooms/{roomId}/messages` (do not send chat text only over the hub).

Planned: return the latest messages from `JoinRoom` to reduce round-trips (not implemented yet).

## Scaling and Redis

| Mode | Behavior |
|------|----------|
| `Redis:Enabled=false` | In-process SignalR groups; fine for single API instance and tests |
| `Redis:Enabled=true` | SignalR Redis backplane syncs groups across API replicas |

See [Global/REDIS.md](../../Global/REDIS.md) for cache, pub/sub, and Streams. **Postgres remains message history; Redis does not store chat transcripts for clients.**
