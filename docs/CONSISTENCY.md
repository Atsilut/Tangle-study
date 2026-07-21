# Consistency and transactions

This MSA does **not** provide distributed ACID. Each service has strong ACID inside its own Postgres **schema** on the shared Compose/Azure database instance (physical database-per-service is deferred — see [ARCHITECTURE.md](ARCHITECTURE.md)). Cross-service work is orchestrated and eventually consistent.

Related implementation issues: [#51](https://github.com/Atsilut/Tangle-study/issues/51)–[#56](https://github.com/Atsilut/Tangle-study/issues/56).

---

## Local ACID

- One schema per service; domain writes use `ExecuteInTransactionAsync` ([`Tangle.AspNetCore.Db`](../services/Tangle.AspNetCore/Db/DbContextTransactionExtensions.cs)).
- Default isolation is Postgres **READ COMMITTED** (Npgsql / EF default when no level is specified). Higher guarantees are not implicit — use conditional updates (`UPDATE … WHERE status = …`), unique constraints, and idempotent remotes.
- Within a service: atomicity, consistency, isolation, and durability apply to committed rows in that schema.

## No distributed 2PC

- Sync HTTP between services is **not** transactional.
- Never call `I*Client` HTTP inside `ExecuteInTransactionAsync` (see [AGENTS.md](AGENTS.md)).

---

## Patterns in use

| Pattern | When | Behavior |
|---------|------|----------|
| **Create saga** | Post/comment/chat message with media (and location for posts) | Persist local row → idempotent remote link/upsert → on failure: logged compensate (unlink → clear location → delete local) |
| **Update saga** | Post PATCH with media/location side effects | Persist local fields → remotes → on failure: reverse remotes (swap media add/remove, clear upserted location) and restore local fields |
| **Remote-first delete** | User, group, post, comment, chat message | Idempotent remote cleanup → local delete/soft-delete transaction |
| **Transactional outbox** | Media `CompleteUpload`, Chat message create, Location cluster enqueue | Outbox row in same DB commit → dispatcher → Redis Streams / pub/sub |
| **Async workers** | Media processing, location cluster, chat jobs | At-least-once Streams; consumers must be idempotent |
| **Reconciliation** | Orphan media links, orphan map pins | Background unlink/delete when remote FK target is gone |

SignalR notifications remain **best-effort** after commit (not outboxed).

Users nickname/delete Redis pub/sub events remain **best-effort** post-commit (not outboxed yet) — cache invalidation may miss on crash between commit and publish.

---

## Flow → consistency level

| Flow | Consistency |
|------|-------------|
| Post create (+ media/location) | Local strong; remotes eventual via saga + compensation |
| Post PATCH (+ media/location) | Local strong; remotes eventual via update saga + compensation |
| Chat message create (+ media) | Local strong; media link after commit; queue/event via outbox |
| Chat message delete | Remote-first media blob cleanup, then local soft-delete |
| User delete | Remote-first detaches (idempotent) then local delete; partial failure → retry / [runbook](DEPLOYMENT.md#partial-delete-recovery-runbook) |
| Media upload complete → Ready | Eventual: conditional `PendingUpload`→`Processing` claim + outbox job → worker → `Ready`/`Failed` |
| Location cluster refresh | Outbox → Streams → worker; public read is cache-then-enqueue |

---

## Rules for new code

1. Never call cross-service HTTP inside a local DB transaction. Compute remote decisions before `ExecuteInTransactionAsync`; keep only DB work inside.
2. Use repositories for entity access; `DbContext` is for transaction scope (and outbox `SaveChanges`) only — see [AGENTS.md](AGENTS.md).
3. Internal detach/delete APIs must be idempotent.
4. Prefer outbox for durable Redis Streams / pub/sub after domain commits (Media, Chat, Location already; extend per service as needed). Document best-effort if intentionally not outboxed.
5. Create and update compensation must log failures (no silent empty `catch`).
6. Deletes: remote-first, then local.
7. State transitions that gate side effects (e.g. media upload complete) must use a conditional update / claim, not check-then-act under READ COMMITTED alone.
8. Compare gateway / internal secrets in constant time (`SecretComparer` / `CryptographicOperations.FixedTimeEquals`).

---

## Testing split

| Layer | What is real | What is faked |
|-------|--------------|---------------|
| **Integration** (`*.Tests`) | In-process HTTP pipeline, Postgres (Testcontainers), Redis where used, outbox dispatcher | Cross-service `I*Client` peers (`InMemoryUserClient`, `FakeMediaClient`, …) |
| **Harness** (`Stack.Tests`) | Full Compose mesh: nginx → gateway → services → Redis → workers | Nothing in the mesh (real JWT, real HTTP between services) |

Both are legitimate. Integration tests prove local ACID / saga / outbox against real infra with controllable peer failures. Harness proves end-to-end cross-service behavior.

---

## Further reading

- [SERVICE_BOUNDARIES.md](SERVICE_BOUNDARIES.md) — create saga notes for Community
- [QUEUE.md](QUEUE.md) — outbox durability
- [EVENTS.md](EVENTS.md) — pub/sub + Chat outbox; Users events best-effort note
- [MEDIA.md](../services/Media/MEDIA.md) — upload state machine
- [GATEWAY.md](../services/Gateway/GATEWAY.md) — identity and internal secret trust model
