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

**Second service on one repository:** `GroupBoardAccessService` and `GroupBoardService` both use `IGroupBoardRepository` — access checks vs CRUD. Do not add further services on the same repo without the same clear split.

## Mapping

- Private `MapToDto` / `MapToResponseDto` in services (not public unless consumed outside the assembly — prefer keeping private).
- Manual mapping only (no AutoMapper).
- Enrich GET DTOs with related data via other **services** (e.g. nicknames from `UserService`).
