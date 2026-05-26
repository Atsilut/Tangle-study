# Api service layer

## Repository boundaries

Each class marked with `[Service]` owns **one** repository field named **`_repo`**, bound to that service’s aggregate (for example `GroupApplicationService` uses only `IGroupApplicationRepository` as `_repo`).

Access to **any other aggregate** — including other types in the same domain folder — goes through that aggregate’s **service**, not its repository.

Cross-domain access follows the same rule (for example `GroupService` calls `PostService`, not `IPostRepository`).

## AppDbContext

Use `AppDbContext` in a service only for **transaction boundaries** (`ExecuteInTransactionAsync`). Do not query another aggregate’s `DbSet` directly (for example `CommentService` must not use `_db.Posts`; use `PostService` instead).

## Circular dependencies

When two services need each other, inject `Lazy<T>` on at least one side (see `GroupApplicationService` / `GroupInvitationService`, `GroupService` / `GroupMembershipService`). The project registers `Lazy<>` in DI via `LazyService<T>`.

## Orchestrator services

Some types coordinate workflows without a primary aggregate (for example `GroupJoinResolutionService`, `GroupJoinService`). They inject **only** other services and `AppDbContext` for transactions — no repositories.
