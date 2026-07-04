# Community service (posts + comments)

Owns `Post` and `Comment` in Postgres schema `community`.

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
| `POST /internal/community/users/{id}/detach-on-deletion` | Api (user delete) |
| `POST /internal/community/groups/{id}/delete-all` | Api (group delete) |

## Outbound

- **Monolith** (`Monolith:BaseUrl`): users, nicknames, mutual blocks, group-board view/write/viewable-keys
- **Media** (`MediaClient:BaseUrl`): link / batch / delete attachments
- **Location** (`LocationClient:BaseUrl`): post geo upsert/clear/list
