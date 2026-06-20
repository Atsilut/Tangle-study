# Api service layer

## Repository boundaries

Each class marked with `[Service]` owns **one** repository field named **`_repo`**, bound to that service’s aggregate (for example `GroupApplicationService` uses only `IGroupApplicationRepository` as `_repo`).

Access to **any other aggregate** — including other types in the same domain folder — goes through that aggregate’s **service**, not its repository.

Cross-domain access follows the same rule (for example `GroupService` calls `PostService`, not `IPostRepository`).

## AppDbContext

Use `AppDbContext` in a service only for **transaction boundaries** (`ExecuteInTransactionAsync`). Do not query another aggregate’s `DbSet` directly (for example `CommentService` must not use `_db.Posts`; use `PostService` instead).

## Async contracts

New Redis Streams jobs require a record in [`Global/Queue/WorkQueueContracts.cs`](Global/Queue/WorkQueueContracts.cs) and a row in [`Global/Queue/QUEUE.md`](Global/Queue/QUEUE.md). New pub/sub events require a record in [`Global/Events/RedisEventContracts.cs`](Global/Events/RedisEventContracts.cs) and a row in [`Global/Events/EVENTS.md`](Global/Events/EVENTS.md). Include `SchemaVersion = 1` on new payload records.

## Circular dependencies

When two services need each other, inject `Lazy<T>` on at least one side (see `GroupApplicationService` / `GroupInvitationService`, `GroupService` / `GroupMembershipService`). The project registers `Lazy<>` in DI via `LazyService<T>`.

## Orchestrator services

Some types coordinate workflows without a primary aggregate (for example `GroupJoinResolutionService`, `GroupJoinService`). They inject **only** other services and `AppDbContext` for transactions — no repositories.

## Reference domains (Posts, Comments, Users)

Use these as templates for new single-aggregate features:

| Concern | Posts | Comments | Users |
|---------|-------|----------|-------|
| CRUD + enriched GET | Default template | Tree/nested `Replies` on GET DTO | Profile + privacy |
| Auth split | — | — | `LoginService` + `LoginController` on `api` |
| Delete orchestration | Transaction + detach | Transaction + detach | Transaction + cross-service detach |

**Intentional exceptions within the reference trio:**

- **Comment GET DTOs** use mutable properties and `[Required]` (not positional records) because the comment tree is built in memory before serialization.
- **Comment GET** exposes `UserId` / `DeletedUserId` (detach fields), not `AuthorNickname` like posts.
- **Empty collections**: Posts and Comments return `null` from list methods → controllers respond with `204 No Content`. Users return a non-null list → `200 OK` with `[]`.
- **Login** returns `null` on failure; other domains throw typed exceptions.

## DTO naming

- **Requests**: `{Aggregate}{Verb}RequestDto` — e.g. `PostCreateRequestDto`, `PostPatchRequestDto`.
- **GET responses**: `{Aggregate}GetResponseDto` — e.g. `PostGetResponseDto`, `FriendshipGetResponseDto`.
- **PATCH responses**: `{Aggregate}PatchResponseDto` when the PATCH body differs from GET.
- **Create-only responses** (when returned): `{Aggregate}{Action}ResponseDto` — e.g. `GroupInvitationCreateResponseDto`.
- **Workflow outcomes**: enum + optional result record in `Dto/` (e.g. `GroupInvitationResult`, `SendFriendRequestOutcome`).
- Multiple related DTOs may live in one file (e.g. `PostRequestDto.cs`); prefer filenames that match the primary type or split when confusing.

**Cross-route DTOs:** Group-board posts are created via `GroupBoardPostController` (Groups API) but use `GroupBoardPostCreateRequestDto` under `Domain/Posts/Dto/` because the aggregate is `Post`. Keep request types with the entity; route ownership stays with the controller folder.

## Repository method naming

Prefix methods with the entity name:

- `GetPostByIdAsync`, `CreatePostAsync`, `DeletePostAsync`
- Not `GetByIdAsync` / `CreateAsync` on new repositories

Existing Groups repositories may retain shorter names; align **Friendships** and **UserBlocks** with the prefixed style.

## HTTP errors

Services throw typed exceptions; `GlobalExceptionHandler` maps them to HTTP status codes. Full convention (401 / 404 / 400 / 409, service patterns, test guidance, known deviations): `.cursor/rules/api-exceptions.mdc`.

## Controllers and HTTP semantics

- Route: `api/{resource}` (login/join under `api` for `LoginController`).
- `[Authorize]` on mutating endpoints; document with `[SwaggerOperation]`.
- Inject concrete `*Service` (no service interfaces).
- **204 No Content** when a service list method returns `null` (Posts, Comments, and most social list endpoints). **200 OK** with an empty array when the service returns a non-null empty list (Users `GetAllUsersAsync`).

## Multi-aggregate folders

**Groups** (`Domain/Groups/`): one bounded context — multiple entities, repositories, services, and controllers. Documented orchestrators (`GroupJoinResolutionService`, `GroupJoinService`) and `GroupJoinPolicyRules` (internal static) are intentional.

**Friendships**: two aggregates (`Friendship`, `FriendRequest`) in one folder; friend-request acceptance is resolved inside `FriendRequestService` (parallel to group join resolution, without a separate resolver type).

**Second service on one repository:** `GroupBoardAccessService` and `GroupBoardService` both use `IGroupBoardRepository` — access checks vs CRUD. `MapPinService` and `LocationClusterService` both use `IMapPinRepository` — pin CRUD vs clustering reads. `LocationSessionService` and `LocationSafetyAlertService` both use `ILocationSessionRepository` — session CRUD vs stale monitor. Do not add further services on the same repo without the same clear split.

## Mapping

- Private `MapToDto` / `MapToResponseDto` in services (not public unless consumed outside the assembly — prefer keeping private).
- Manual mapping only (no AutoMapper).
- Enrich GET DTOs with related data via other **services** (e.g. nicknames from `UserService`).

## N+1 queries

Repositories return flat entities; services batch-enrich via other services. Lazy loading is **not** enabled (`UseLazyLoadingProxies` is not configured). N+1 bugs here are **loops that `await` per-item service/repo calls**, not accidental navigation-property loads.

### Rule

In `MapManyAsync` and other list endpoints, **batch all enrichment before the loop**. Never `await` a per-item service or repository call inside `foreach`.

```csharp
// Good — batch first, sync map
var nicknames = await _userService.GetNicknamesByUserIdsAsync(items.Select(i => i.AuthorUserId));
var mediaById = await _mediaService.GetMediaByCommentIdsAsync(items.Select(i => i.Id).ToList());
return [.. items.Select(i => MapToDto(i, nicknames[...], mediaById.GetValueOrDefault(i.Id)))];

// Bad — N round trips
foreach (var item in items)
    results.Add(await MapToDtoAsync(item, ...)); // awaits DB inside loop
```

Single-item GET (`GetPostByIdAsync`, `GetCommentByIdAsync`) may use one enrichment call per field — that is not N+1.

Prefer batch `WHERE ... IN (...)` over JOINs for cross-aggregate enrichment. JOIN saves at most one round trip; batch `IN` fixes query count. Do not use lazy loading — it hides queries and bypasses `NicknameCacheService` / `MediaService` boundaries.

Use explicit `Include` only when the graph is one aggregate and always needed together (e.g. `ChatRoom.Participants` in `ChatRoomRepository`).

### Reference implementations

| Pattern | File |
|---------|------|
| List + nicknames + media batch | `CommentService.BuildCommentTreeAsync` |
| List + nicknames + media batch | `ChatMessageService.MapManyAsync` |
| Batch lookup by IDs | `UserRepository.GetNicknamesByIdsAsync` |
| Batch media by FK | `MediaAssetRepository.GetMediaAssetByCommentIdsAsync` |

### Inventory

Update the **Status** column when a fix group lands.

| ID | Domain | Location | Trigger | Status |
|----|--------|----------|---------|--------|
| P-1 | Posts | `PostService.MapManyAsync` | Post list GETs | fixed |
| P-2 | Posts | `PostService.DeleteAllByGroupAsync` → `MediaService.DeleteBlobStorageForPostsAsync` | Group delete | fixed |
| M-1 | Media | `GetMediaAssetsByPostIdsAsync` / `GetMediaByPostIdsAsync` | Post list + group delete | fixed |
| M-2 | Media | `MediaService.DeleteBlobStorageForAssetsAsync` | Bulk blob cleanup (parallel, max 8 concurrent) | fixed |
| G-1 | Groups | `GroupBoardService.ListAsync` → `GroupBoardAccessService.FilterViewableBoardsAsync` | Board list | fixed |
| G-2 | Groups | `GroupRepository.GetGroupNamesByIdsAsync` | Invitation list | fixed |
| G-3 | Groups | `GroupMembershipService.HandleUserDeletionAsync` | User delete (rare) | fixed |
| C-1 | Chat | `ChatRoomAccessService.EnsureCanCreatePlatformGroupRoomAsync` | Create platform room | fixed |
| C-2 | Chat | `ChatRoomAccessService.EnsureCanCreateMultiRoomAsync` | Create multi room | fixed |
| C-3 | Chat | `ChatRoomAccessService.EnsureInviteeCanBeAddedAsync` | Add participant | fixed |
| U-1 | Users | `NicknameCacheService.GetNicknamesByUserIdsAsync` | Redis MGET/pipeline; parallel cache I/O fallback | fixed |
| CM-1 | Comments | `CommentService.GetCommentByIdAsync` | Single-comment GET only | fixed |
| L-1 | Location | `LocationAccessService.FilterViewableMapPinsAsync` | Map bbox GET | fixed |
| L-2 | Location | `LocationSessionService` live sharing lists | Active locations + sharing status | fixed |
| L-3 | Location | `LocationSafetyAlertService` stale/SOS recipients | Safety alerts | fixed |
| P-3 | Posts | `PostService.GetGroupBoardContextsByPostIdsAsync` | User comment list | fixed |
| G-4 | Groups | `GroupBoardAccessService.ResolveViewableBoardKeysAsync` | Post search by nickname | fixed |
| C-4 | Chat | `ChatMessageService.MarkMessagesSeenAsync` | Mark seen | fixed |

**Clean (no action):** Friendships and UserBlocks list paths; `ChatMessageService` list mapping; `GetAllUsersAsync`; single-item GET enrichment (`GetPostByIdAsync`, `GetCommentByIdAsync`).

### Fix groups

Fix together; suggested order is 1 → 2 → 3, then 4 only if profiling warrants it.

| Group | Issues | Fix summary |
|-------|--------|-------------|
| **1 — Post media** | M-1, P-1, P-2 | Add `GetMediaAssetsByPostIdsAsync` / `GetMediaByPostIdsAsync`; refactor `PostService.MapManyAsync` and `DeleteBlobStorageForPostsAsync`. Copy comment/chat batch in `MediaAssetRepository`. |
| **2 — Groups** | G-1, G-2, (G-3) | `FilterViewableBoardsAsync` (load group + member once); repo `GetGroupNamesByIdsAsync` with `WHERE Id IN (...)`; optional `GetMembersByGroupIdsAsync` on user delete. |
| **3 — Chat validation** | C-1, C-2, C-3 | Batch block/exists/membership helpers on `UserBlockService`, `UserService`, `GroupMembershipService`; refactor `ChatRoomAccessService` loops. Preserve error messages. |
| **4 — Infra (optional)** | U-1, M-2 | Redis pipeline / MGET for nicknames; bounded parallel blob deletes. Not EF N+1. |

| Dimension | Group 1 | Group 2 | Group 3 | Group 4 |
|-----------|---------|---------|---------|---------|
| Layer | Missing repo batch | Redundant re-fetch in auth loop | Per-pair checks in loops | External I/O |
| HTTP impact | Hot read feeds | Board / invitation lists | Room create / add | All nickname reads |
| Risk | Low | Medium (auth equivalence) | Medium (error semantics) | Low priority |
