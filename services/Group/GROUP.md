# Group service

Owns groups, boards, memberships, invitations, applications, and blacklist in Postgres schema `group`.

## Public routes

| Prefix | Notes |
|--------|--------|
| `/api/groups` | Group CRUD, join, me, transfer |
| `/api/groups/{id}/members` | Membership list/role/remove |
| `/api/groups/{id}/boards` | Board CRUD |
| `/api/groups/{id}/invitations` | Group invitations |
| `/api/groups/{id}/applications` | Group applications |
| `/api/groups/{id}/blacklist` | Blacklist |
| `/api/invitations/*` | Accept/reject/ignore/me |
| `/api/applications/*` | Approve/reject/ignore/me |

Cross-routes owned by other services (nginx routes them first):

| Route | Owner |
|-------|--------|
| `/api/groups/{id}/boards/{id}/posts` | Community |
| `/api/groups/{id}/chat-rooms` | Chat |

## Internal routes (`X-Internal-Secret`)

| Route | Callers |
|-------|---------|
| `POST /internal/group/{id}/exists` | Chat |
| `POST /internal/group/{id}/members/validate` | Chat |
| `POST /internal/group/{id}/members/{userId}/validate` | Chat, Location |
| `GET /internal/group/{id}/membership/me` | Chat |
| `GET /internal/group/{id}/members/for-member` | Location |
| `GET /internal/group/{id}/member-ids` | Location |
| `POST /internal/group/{id}/boards/{boardId}/validate-view` | Community |
| `POST /internal/group/{id}/boards/{boardId}/validate-write` | Community |
| `POST /internal/group/boards/viewable-keys` | Community |
| `POST /internal/group/users/{id}/detach-on-deletion` | Users-service |

## Outbound

- **Users** (`Users:BaseUrl`): users, nicknames, block checks
- **Community** (`CommunityClient:BaseUrl`): delete-all posts on group delete
- **Location** (`LocationClient:BaseUrl`): end sessions on group delete

## Schema migration (local Compose)

Group tables live in Postgres schema `group`. The monolith migration `RemoveMonolithGroupTables` drops legacy `public` group tables **without copying data**.

**Local upgrade:** wipe the Postgres volume before first boot of group-service on an existing stack:

```bash
docker compose down -v
docker compose up --build
```

Fresh volumes start empty in `group` schema. Azure cutover (when added) needs an explicit data-move plan; Compose study stacks are expected to reset.

## Internal membership error messages

Single-member validate (`POST .../members/{userId}/validate`) accepts an optional JSON body `{ "notFoundMessage": "..." }` (default `"Group not found"`) so Chat/Location can hide membership. Batch validate accepts `{ "userIds": [...], "errorMessage": "..." }`.
