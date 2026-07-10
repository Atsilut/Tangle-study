# Users service

Identity, authentication, JWT issuance, and internal user-access API. Orchestrates user delete across all domain services.

## Public routes

| Prefix | Notes |
|--------|--------|
| `/api/login` | Sign in; returns JWT |
| `/api/join` | Register; returns JWT |
| `/api/users` | List users, profile CRUD, delete account |

Swagger (via nginx → gateway): `http://localhost:8080/api`.

## Internal routes (`X-Internal-Secret`)

| Route | Callers |
|-------|---------|
| `/internal/users/*` | All domain services via `IUserClient` (existence, nicknames, friends-list visibility, block checks) |

## Auth flow

1. **Login/join** — Users service validates credentials and issues JWT via `TokenProvider` (`security.yml`).
2. **Gateway** — Validates Bearer JWT on subsequent requests; forwards `X-User-Id` + `X-Gateway-Secret`.
3. **Domain services** — Trust gateway headers via `GatewayIdentityAuthenticationHandler` (no JWT validation in the request path).

See [GATEWAY.md](../Gateway/GATEWAY.md).

## User delete orchestration

`UserService.DeleteUserAsync` calls detach/delete endpoints on extracted services:

- Community, Media, Chat, Group, Social, Location (via `I*Client` HTTP clients)

Each service exposes `/internal/{domain}/users/{id}/detach-on-deletion`.

## Data

Postgres: `users` schema (`Users` table). Migrations run on startup in Development/Docker.

## Configuration

| Key | Purpose |
|-----|---------|
| `security.yml` | JWT issuer, audience, expiry, secret |
| `UsersOptions` / `Users:BaseUrl` | Self-reference for internal routes |
| `GatewayIdentityOptions` | Expected `X-Gateway-Secret` from gateway |
| `*Client:BaseUrl` | Outbound HTTP clients for user-delete detach |

## Tests

Integration and unit tests: `services/Users.Tests/` (replaces most former `Api.Tests` identity/boundary tests).

## Related docs

- [GATEWAY.md](../Gateway/GATEWAY.md)
- [SERVICE_BOUNDARIES.md](../../docs/SERVICE_BOUNDARIES.md#users-service)
- [MSA step 7](../../docs/MSA_MIGRATION.md#step-7--users-service--gateway-develop-compose-done-azure-open)
