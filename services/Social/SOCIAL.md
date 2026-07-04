# Social service (friendships + user-blocks)

Owns `Friendship`, `FriendRequest`, and `UserBlock` in Postgres schema `social`.

Block and friend-request flows are coupled (`IgnoredByBlock`, block deletes outgoing pending requests). Group **blacklist** stays in Group.

## Public routes

| Prefix | Notes |
|--------|--------|
| `/api/friendships` | List friends, privacy-gated list, unfriend |
| `/api/friendships/requests` | Send / accept / ignore / reject / cancel |
| `/api/users/blocks` | Block / list / unblock |

## Internal routes (`X-Internal-Secret`)

| Route | Callers |
|-------|---------|
| `POST /internal/social/friendships/validate-pair` | Chat (DM requires friendship) |
| `POST /internal/social/blocks/validate-between` | Chat |
| `POST /internal/social/blocks/validate-against-others` | Chat |
| `POST /internal/social/blocks/mutual-ids` | Community, Location |
| `POST /internal/social/blocks/is-blocked-by` | Group |
| `POST /internal/social/users/{id}/detach-on-deletion` | Api (user delete) |

## Outbound

- **Monolith** (`Monolith:BaseUrl`): user existence, nicknames, friends-list visibility

## Dev data

Initial migration creates empty `social` tables. Legacy `public."Friendships"` / `"FriendRequests"` / `"UserBlocks"` are dropped by Api migration `RemoveMonolithSocialTables` **without copying rows**. Reset the Compose Postgres volume when upgrading an existing DB:

```bash
docker compose down -v
docker compose up --build
```
