# Consistency and transactions

This MSA does **not** provide distributed ACID. Each service has strong ACID inside its own Postgres schema; cross-service work is orchestrated and eventually consistent.

Related implementation issues: [#51](https://github.com/Atsilut/Tangle-study/issues/51)–[#56](https://github.com/Atsilut/Tangle-study/issues/56).

---

## Local ACID

- One schema per service; domain writes use `ExecuteInTransactionAsync` ([`Tangle.AspNetCore.Db`](../services/Tangle.AspNetCore/Db/DbContextTransactionExtensions.cs)).
- Within a service: atomicity, consistency, isolation, and durability apply to committed rows.

## No distributed 2PC

- Sync HTTP between services is **not** transactional.
- Never call `I*Client` HTTP inside `ExecuteInTransactionAsync` (see [AGENTS.md](AGENTS.md)).

---

## Patterns in use

| Pattern | When | Behavior |
|---------|------|----------|
| **Create saga** | Post/comment/chat message with media (and location for posts) | Persist local row → idempotent remote link/upsert → on failure: logged compensate (unlink → clear location → delete local) |
| **Remote-first delete** | User, group, post, comment | Idempotent remote cleanup → local delete transaction |
| **Transactional outbox** | Media `CompleteUpload`, Chat message create (queue + event) | Outbox row in same DB commit → dispatcher → Redis Streams / pub/sub |
| **Async workers** | Media processing, location cluster, chat jobs | At-least-once Streams; consumers must be idempotent |
| **Reconciliation** | Orphan media links, orphan map pins | Background unlink/delete when remote FK target is gone |

SignalR notifications remain **best-effort** after commit (not outboxed).

---

## Flow → consistency level

| Flow | Consistency |
|------|-------------|
| Post create (+ media/location) | Local strong; remotes eventual via saga + compensation |
| Chat message create (+ media) | Local strong; media link after commit; queue/event via outbox |
| User delete | Remote-first detaches (idempotent) then local delete; partial failure → retry / [runbook](DEPLOYMENT.md#partial-delete-recovery-runbook) |
| Media upload complete → Ready | Eventual: `PendingUpload` → `Processing` (outbox job) → worker → `Ready`/`Failed` |

---

## Rules for new code

1. Never call cross-service HTTP inside a local DB transaction.
2. Internal detach/delete APIs must be idempotent.
3. Prefer outbox for durable Redis Streams / pub/sub after domain commits (Media/Chat already; extend per service as needed).
4. Create compensation must log failures (no silent empty `catch`).
5. Deletes: remote-first, then local.

---

## Further reading

- [SERVICE_BOUNDARIES.md](SERVICE_BOUNDARIES.md) — create saga notes for Community
- [QUEUE.md](QUEUE.md) — outbox durability
- [EVENTS.md](EVENTS.md) — pub/sub + Chat outbox
- [MEDIA.md](../services/Media/MEDIA.md) — upload state machine
