# Social service (friendships + user-blocks)

Owns `Friendship`, `FriendRequest`, and `UserBlock` in Postgres schema `social`. Layout follows the horizontal extracted-service template (`Api/`, `Service/`, `Repository/`, `Entities/`, `Dto/` — see [`docs/SERVICE_BOUNDARIES.md`](../../docs/SERVICE_BOUNDARIES.md)).

Block and friend-request flows are coupled (`IgnoredByBlock`, block deletes outgoing pending requests). Group **blacklist** stays in Group.

## Public routes

| Prefix | Notes |
|--------|--------|
| `/api/friendships` | List friends, privacy-gated list, unfriend |
| `/api/friendships/requests` | Send / accept / ignore / reject / cancel |
| `/api/users/blocks` | Block / list / unblock |

## Internal routes (`X-Internal-Secret`)

Subject user ids are always explicit in the request body (not taken from JWT). Callers pass the user being checked so invitee / background paths work without impersonating the HTTP caller.

| Route | Body | Callers |
|-------|------|---------|
| `POST /internal/social/friendships/validate-pair` | `{ userId, otherUserId }` | Chat (DM requires friendship) |
| `POST /internal/social/blocks/validate-between` | `{ userId, otherUserId }` | Chat |
| `POST /internal/social/blocks/validate-against-others` | `{ userId, otherUserIds }` | Chat |
| `POST /internal/social/blocks/mutual-ids` | `{ userId, otherUserIds }` | Community, Location |
| `POST /internal/social/blocks/is-blocked-by` | `{ blockerUserId, blockedUserId }` | Group |
| `POST /internal/social/users/{id}/detach-on-deletion` | — | Users-service (transactional) |

## Outbound

- **Users** (`Users:BaseUrl`): user existence, nicknames, friends-list visibility

## Dev data

Initial migration creates empty `social` tables. Legacy `public."Friendships"` / `"FriendRequests"` / `"UserBlocks"` are dropped by Api migration `RemoveMonolithSocialTables` **without copying rows**. Reset the Compose Postgres volume when upgrading an existing DB:

```bash
docker compose down -v
docker compose up --build
```
