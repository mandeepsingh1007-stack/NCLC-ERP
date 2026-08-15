---
name: Security and Tenancy Implementation
description: Phase 5 implementation guidance for JWT auth, RBAC, tenant isolation, CRUD mutations, and session management
---

# Security and Tenancy Implementation Skill

## Phase 5 Scope

Implement the security layer of the No-Code/Low-Code platform:
- JWT authentication with ASP.NET Core Identity or custom auth
- RBAC with hierarchical scope (Client → Org → Window → Process → Table → Column → Record)
- Tenant isolation via QueryBuilder predicate injection
- CRUD mutations (POST/PUT/DELETE) — remove 501 stubs
- Session management with refresh tokens and deny list
- Audit logging (SysAccessLog)

## Governing ADRs

- **ADR-002** (Authentication): ASP.NET Core Identity + JWT bearer tokens
- **ADR-011** (RBAC/Authorization): Hierarchical RBAC model, 14 security tables
- **ADR-012** (Session Management): Short-lived JWT + Redis deny list + SysSession

## Non-Negotiable Rules

1. **UI is never the security boundary.** Permissions enforced server-side only.
2. **All data queries include tenant predicates.** QueryBuilder must inject ClientId/OrgId automatically from claims.
3. **No client-side authorization logic.** UI visibility hints are cosmetic only.
4. **Tenants must be isolated at the database level.** WHERE ClientId = @Claims.ClientId on every query.
5. **JWT tokens are stateless; revocation uses Redis deny list.**
6. **POST/PUT/DELETE must return proper HTTP status codes** (201, 200, 204, 400, 401, 403).
7. **No hardcoded credentials or secrets.** Use environment variables or ASP.NET Core Secret Manager.

## Implementation Order

### Sprint 1: Foundation
1. Create 14 security metadata tables (migration)
2. Implement JWT middleware + claims principal
3. Implement auth controller (login, refresh, logout)
4. Implement basic policy (authenticated = access)

### Sprint 2: RBAC
5. Create Dapper repositories for security tables
6. Implement role-to-permission resolution service
7. Implement ASP.NET Core policies for each scope level
8. Add QueryBuilder tenant predicate injection

### Sprint 3: CRUD + Session
9. Remove 501 stubs from POST/PUT/DELETE endpoints
10. Add RBAC enforcement to mutation endpoints
11. Implement SysSession table + refresh token flow
12. Implement SysAccessLog audit logging

### Sprint 4: Hardening
13. Concurrent session limits
14. Token revocation via deny list
15. Frontend auth guard (optional, cosmetic)
16. Tests: auth, RBAC, mutation, regression

## Testing Requirements

- Auth unit tests: valid/invalid login, token expiry, refresh rotation
- RBAC unit tests: role bypass, tenant isolation, record-level access
- Mutation integration tests: POST/PUT/DELETE with RBAC enforcement
- Regression: Phase 1-4 tests still pass

## Security Review Checklist

- [ ] No SQL injection in mutation SQL construction
- [ ] No client-side authorization assumptions
- [ ] No secrets in source code or config
- [ ] Passwords hashed with bcrypt/argon2 (never plaintext)
- [ ] CSRF protection considered (if using cookies)
- [ ] Rate limiting on auth endpoints
- [ ] JWT signing key from environment (never hardcoded)

## Related Agents

- **architect**: Design security schema and policy boundaries
- **backend-developer**: Implement auth, RBAC, CRUD mutations
- **database-engineer**: Security table schema, indexes, constraints
- **security-reviewer**: Authorization bypass, tenant isolation, injection
- **qa-engineer**: Auth/RBAC/mutation test suite
