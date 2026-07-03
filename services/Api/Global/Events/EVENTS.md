# Domain events (Redis pub/sub)

Fire-and-forget cross-service notifications via `IEventPublisher`. Payload types live in [`RedisEventContracts.cs`](RedisEventContracts.cs); channel names in [`RedisEventChannels.cs`](RedisEventChannels.cs).

Unlike [Redis Streams](../Queue/QUEUE.md), pub/sub is **not durable** — subscribers must tolerate missed messages if offline. Client realtime delivery uses **SignalR**, not pub/sub.

## Events

| Channel (`RedisEventChannels`) | Payload | `schemaVersion` | Published when |
|--------------------------------|---------|-----------------|----------------|
| `tangle:events:chat.message.created` | `ChatMessageCreatedEvent` | `1` | After a chat message is persisted (`ChatMessageService`) |
| `tangle:events:user.nickname.changed` | `UserNicknameChangedEvent` | `1` | On nickname update or user delete (`UserService`) |

### `ChatMessageCreatedEvent`

| Field | Type | Notes |
|-------|------|-------|
| `schemaVersion` | `int` | Always `1` for this shape |
| `messageId` | `long` | Persisted message ID |
| `chatRoomId` | `long` | Room the message belongs to |
| `senderUserId` | `long` | Author |
| `body` | `string` | Message text at publish time |
| `sentAt` | `DateTimeOffset` | UTC |

Example JSON:

```json
{
  "schemaVersion": 1,
  "messageId": 42,
  "chatRoomId": 7,
  "senderUserId": 3,
  "body": "Hello",
  "sentAt": "2026-06-20T12:00:00+00:00"
}
```

### `UserNicknameChangedEvent`

| Field | Type | Notes |
|-------|------|-------|
| `schemaVersion` | `int` | Always `1` for this shape |
| `userId` | `long` | User whose nickname changed |
| `nickname` | `string?` | New nickname; `null` when deleted |
| `isDeleted` | `bool` | `true` when the user account was deleted |
| `occurredAt` | `DateTimeOffset` | UTC |

Example JSON:

```json
{
  "schemaVersion": 1,
  "userId": 5,
  "nickname": "alice",
  "isDeleted": false,
  "occurredAt": "2026-06-20T12:00:00+00:00"
}
```

## Subscribers

[`RedisEventSubscriberHostedService.cs`](RedisEventSubscriberHostedService.cs) subscribes to both channels and **logs** received payloads. No downstream handler runs in the monolith today — the wiring exists so extracted services can subscribe without changing producers.

When `Redis:Enabled=false`, `NoOpEventPublisher` is registered and nothing is published.

## Schema versioning

Every payload includes `schemaVersion` (starting at `1`). Consumers should:

- Reject or route unknown major versions explicitly when breaking changes land.
- Ignore unknown JSON fields (forward compatibility).

Bump `schemaVersion` when removing or renaming fields. Add optional fields without bumping when old consumers can ignore them.

See also: [QUEUE.md](../Queue/QUEUE.md) (Streams jobs use the same convention).

## Relation to Streams and SignalR

| Mechanism | Purpose |
|-----------|---------|
| SignalR + backplane | Live chat UI to connected clients |
| Redis pub/sub (`IEventPublisher`) | Optional cross-service notifications (this doc) |
| Redis Streams (`IWorkQueue`) | Durable async jobs for workers |

Do not use pub/sub as the client realtime channel or as durable work queue.

## Related docs

- [REDIS.md](../REDIS.md) — Redis configuration and data flows
- [QUEUE.md](../Queue/QUEUE.md) — Streams job contracts
- [CHAT.md](../../../services/Chat/CHAT.md) — chat REST and SignalR hub
