# Community service (posts + comments)

Owns `Post` and `Comment` in Postgres schema `community`.

## List ordering

| List | Order |
|------|--------|
| Posts (platform, group board, by author) | `CreatedAt` desc, then `Id` desc (newest first) |
| Comments on a post (tree roots and replies) | `CreatedAt` asc, then `Id` asc (chronological) |
| Comments by user | `CreatedAt` desc, then `Id` desc (newest first) |

## Public routes

| Prefix | Notes |
|--------|--------|
| `/api/posts` | Platform posts CRUD |
| `/api/comments` | Comments CRUD + trees |
| `/api/groups/{groupId}/boards/{boardId}/posts` | Group-board posts (aggregate is Post) |

## Internal routes (`X-Internal-Secret`)

| Route | Callers |
|-------|---------|
| `POST /internal/community/{id}/media-view` | Media |
| `POST /internal/community/comments/{id}/media-view` | Media |
| `POST /internal/community/{id}/validate-owner` | Location |
| `POST /internal/community/viewable-ids` | Location |
| `POST /internal/community/users/{id}/detach-on-deletion` | Users-service |
| `POST /internal/community/groups/{id}/delete-all` | Group-service |

## Outbound

- **Users** (`Users:BaseUrl`): users, nicknames, mutual blocks, group-board view/write/viewable-keys
- **Media** (`MediaClient:BaseUrl`): link / batch / delete attachments
- **Location** (`LocationClient:BaseUrl`): post geo upsert/clear/list
