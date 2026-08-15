# ADR-011: RBAC / Authorization Model

- **Status**: Accepted
- **Date**: 2026-08-16
- **Related**: ADR-002 (Authentication), HLD/LLD Section 15 (Security)

## Context

Phase 5 implements multi-client, multi-organization, role-based access control.
The HLD/LLD defines 14 security metadata tables (SysClient, SysOrg, SysUser, SysRole,
SysRoleClient, SysRoleOrg, SysRoleWindow, SysRoleProcess, SysRoleTable, SysRoleColumn,
SysRoleRecord, SysRolePrivate, SysSession, SysAccessLog).

## Decision

Use **hierarchical RBAC with multi-tenant scope** where permissions cascade from client → org → window → process → table → column → record.

### Security Scope Hierarchy

```
Client (tenant)
  → Organization (sub-tenant within client)
    → Window (screen access)
      → Process (workflow access)
        → Table (data access)
          → Column (field-level access)
            → Record (row-level access)
```

### Permission Types

Each level controls: `None`, `ReadOnly`, `ReadWrite`, `Create`, `FullControl`.

### Enforcement Model

1. **Server-side enforcement only** — UI visibility never equals permission.
2. **Claims-based token** — JWT contains ClientId, OrgId (if applicable), RoleIds.
3. **Policy-based authorization** — ASP.NET Core policies enforce each level.
4. **Tenant predicate injection** — QueryBuilder automatically appends tenant/org predicates.
5. **Record-level filter** — SysRoleRecord applies row-level filters at query time.
6. **Session tracking** — SysSession table tracks active sessions; token revocation via deny list.

### Token Design

- Access token: 15-minute TTL (short-lived JWT).
- Refresh token: 7-day sliding TTL, stored server-side in SysSession.
- Revocation: deny list in Redis (hashed JWT jti); checked on refresh and optional on access.

### Multi-Tenant Isolation

- Every data query includes `WHERE ClientId = @Claims.ClientId`.
- Org-scoped queries add `WHERE OrgId = @Claims.OrgId` when OrgId is present in claims.
- Cross-client access blocked by policy; cross-org access allowed only if user has OrgRole.

## Alternatives Considered

- **ABAC (Attribute-Based)**: More flexible but significantly more complex to audit. RBAC sufficient for Phase 5.
- **Policy-as-code (OPA/Rego)**: Overhead not justified for expected rule count. Keep policies in DB metadata.
- **JWT-only (no deny list)**: Stateless but no logout capability. Deny list chosen for usability.

## Consequences

- Requires creating 14 security metadata tables (migration).
- Requires modifying QueryBuilder to inject tenant predicates from claims.
- Requires JWT middleware, claims principal setup, and ASP.NET Core policies.
- Frontend must respect server-side permissions; no client-side permission enforcement.
- ADR-008 (Session Management) complements this ADR for session lifecycle.
