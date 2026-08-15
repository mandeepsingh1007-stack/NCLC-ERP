# ADR-012: Session Management

- **Status**: Accepted
- **Date**: 2026-08-16
- **Related**: ADR-002 (Authentication), ADR-011 (RBAC)

## Context

JWT tokens are stateless by design, but the platform needs:
1. User-initiated logout
2. Server-side token revocation
3. Session tracking for audit/compliance
4. Concurrent session limits per user

## Decision

Use **short-lived JWT access tokens + server-side session store + Redis deny list**.

### Token Lifecycle

| Token | TTL | Storage | Revocable |
|---|---|---|---|
| Access (JWT) | 15 min | Client (localStorage/cookie) | Via deny list |
| Refresh | 7 days | SysSession table + HttpOnly cookie | Via session delete |

### Session Storage

- **SysSession table** (PostgreSQL): tracks `UserId`, `TokenJti`, `IpAddress`, `UserAgent`, `CreatedAt`, `ExpiresAt`, `RevokedAt`.
- **Redis deny list**: hashed JWT `jti` values for active revocations; 15-minute TTL matching access token.
- **Slide on refresh**: Refresh token expiry extends each time it is used (up to 7 days).

### Concurrent Session Limits

- Default: 5 concurrent sessions per user.
- Limit enforced at login: count active sessions; revoke oldest if over limit.
- Admin override: sysadmin can force-logout all sessions for a user.

### Logout Flow

1. Client sends `POST /auth/logout` with current refresh token.
2. Server revokes refresh token in SysSession (marks `RevokedAt`).
3. Server adds JWT `jti` to Redis deny list (15-minute TTL).
4. Server returns 204 No Content.

### Audit

- All login/logout/session-creation events logged to `SysAccessLog`.
- Suspicious activity (multiple IPs, rapid refresh) flagged for review.

## Consequences

- Adds Redis dependency beyond cache (deny list).
- SysSession table needs cleanup job (Hangfire) for expired sessions.
- Access token TTL is short — refresh flow must be seamless in frontend.
