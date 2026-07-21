# Gateway service

YARP reverse proxy and JWT validation at the Compose edge. The gateway routes traffic and establishes caller identity — it does **not** own domain business logic.

## Role

| Responsibility | Owner |
|----------------|-------|
| Path routing to domain services | Gateway (YARP) |
| Bearer JWT validation | Gateway (`GatewayAuthMiddleware`) |
| JWT issuance (login/join) | Users service (`TokenProvider`) |
| Domain logic | Media, Chat, Location, Community, Group, Social, Users |

## Request flow

```text
Browser → nginx:8080
  └─ /api/*, /hubs/* → gateway:8080
       ├─ GatewayAuthMiddleware (JWT or anonymous allow-list; rejects /internal/*)
       ├─ Sets X-User-Id + X-Gateway-Secret on authenticated requests
       └─ YARP forwards to target cluster (users, media, chat, …)

Domain service → GatewayIdentityAuthenticationHandler
  └─ Trusts X-Gateway-Secret; builds ClaimsPrincipal from X-User-Id
```

`/internal/*` is **not** routed through the gateway: services call each other directly on the private
network (e.g. `http://media:8080/internal/media/*`) authenticated with `X-Internal-Secret`. The edge
(nginx) does not proxy `/internal/*`, and `GatewayAuthMiddleware` returns `404` for any `/internal/*`
that reaches it. See [SERVICE_BOUNDARIES.md](../../docs/SERVICE_BOUNDARIES.md#internal-service-authentication).

## YARP routes

Routes and cluster destinations are in [`appsettings.json`](appsettings.json). Clusters map to Compose service names (`http://users:8080/`, `http://media:8080/`, etc.).

Higher-order routes (e.g. group-board posts, group chat rooms, social blocks) are registered before catch-all service routes so cross-service paths reach the correct cluster.

## Anonymous paths

[`GatewayAuthMiddleware`](Middleware/GatewayAuthMiddleware.cs) allows unauthenticated access to:

- `/api/login`, `/api/join`, `/api/join/nickname-available`
- `GET /api/users`, `GET /api/users/{id}`
- `GET /api/media/{id}/content` (public linked content)
- `GET /api/posts/*`, `GET /api/comments/*` (public reads)
- `/health`

All other `/api/*` and `/hubs/*` requests require a valid Bearer JWT (or `?access_token=` for SignalR negotiate).

## Configuration

| File / key | Purpose |
|------------|---------|
| `security.yml` | JWT issuer, audience, secret (must match Users service) |
| `Gateway:Secret` | Shared secret forwarded as `X-Gateway-Secret` to downstream services |
| `ReverseProxy` | YARP routes and cluster addresses |

## Trust and secrets

- **Gateway identity:** domain services trust `X-User-Id` only when `X-Gateway-Secret` matches `GatewayIdentity:Secret`. Compromising the gateway secret impersonates any user to every service that accepts gateway identity.
- **Internal mesh:** `/internal/*` uses **per-callee** secrets — each service's `InternalAccess:Secret` is unique; callers send that callee's value as `X-Internal-Secret`. A leak of one process only exposes peers that process is configured to call. Prefer private networking (Compose / Container Apps ingress) and rotate secrets independently. Optional later hardening: mTLS / SPIFFE (not implemented).
- **Constant-time compare:** both gateway and internal secret checks use `SecretComparer` (`CryptographicOperations.FixedTimeEquals`) — do not reintroduce `string ==` / `!=` for secrets.

## Related docs

- [USERS.md](../Users/USERS.md) — login, JWT issuance, user delete orchestration
- [MSA step 7](../../docs/MSA_MIGRATION.md#step-7--users-service--gateway)
- [ARCHITECTURE.md](../../docs/ARCHITECTURE.md)
- [CONSISTENCY.md](../../docs/CONSISTENCY.md) — identity vs ACID boundaries
