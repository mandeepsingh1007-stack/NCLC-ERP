# Active Agent State

## Current Phase
5 — Security and Tenancy

## Phase Status
IMPLEMENTATION COMPLETE — REMEDIATION CONTINUES (Wave 3)

## Phase 4 — ACCEPTED (2026-08-16)
- **React Runtime: COMPLETE**
- CI: GREEN
- Build: 0 warnings, 0 errors
- Display logic parity: 20/20 PASS
- Frontend tests: 100/100 PASS
- .NET unit tests: 216/216 PASS
- Bundle: 119 KB gzipped (target ≤250 KB)
- Components: DynamicField, DynamicForm, FieldGroup, LookupField, DynamicGrid, MainWindow, MainGrid, MenuNavigation
- Infrastructure: API client, TanStack Query, display logic evaluator (no eval/new Function), lazy loading, error/loading/empty states
- Phase 4 commit: 451a658

## Phase 5 — IMPLEMENTATION COMPLETE (2026-08-16)
- **Implementation: COMPLETE**
- **Build: PASS** — 0 warnings, 0 errors (full solution)
- **Unit Tests: PASS** — 290/290 (all Platform.Tests.Core)
- **Phase 5 Features Implemented:**

### Authentication
- JWT access tokens (15 min TTL) via System.IdentityModel.Tokens.Jwt
- Refresh tokens (7 day TTL) with rotation
- BCrypt-compatible password hashing
- Redis deny list for token revocation (hashed JTI)
- Session management via SysSession table
- AuthService with login, refresh, logout, change-password

### RBAC (Hierarchical)
- 14 security metadata tables (migration 005)
- IPermissionService with IMemoryCache (10 min TTL, keyed by ClientId+RoleIds)
- IRbacRepository interface in Core, Dapper implementation in Data
- INamespaceRepository interface in Core, Dapper implementation in Data
- Cascade: Column > Table > Window fallback
- Permission levels: None(0), ReadOnly(1), ReadWrite(2), Create(3), FullControl(4)

### Tenant Isolation
- QueryBuilder injects tenant/org predicates from IReadOnlyContext
- @ClientId/@OrgId parameter binding
- Tenant predicates applied to SELECT, DELETE mutations
- FilterParser for client-side filter DSL → SQL

### CRUD Mutations
- POST/PUT/DELETE endpoints in Data API (replaced 501 stubs)
- BuildInsert, BuildUpdate, BuildDelete with parameterized SQL
- Writable column filtering (IsUpdateable check)
- POLifecycleManager integration

### Security Metadata Repositories
- SysUserRepository, SysRoleRepository
- RbacRepository (batch resolution)
- NamespaceRepository (window/table/column name→ID)

### Lookup Security (Wave 2.3)
- Authentication enforced on all lookup endpoints
- Authorization: CanReadTableAsync + CheckColumnAsync
- Tenant/org isolation via context predicates
- Regression tests: 5 negative security tests

### Tests
- AuthTests: 10 tests (JWT generation, claims, refresh tokens, hashing, permission levels)
- QueryBuilderTenantTests: 18 tests (SELECT/INSERT/UPDATE/DELETE, tenant predicates, validation)
- SecurityNegativeTests: Negative cases for auth, RBAC, lookup, tenant isolation
- Existing test suite: 216+ tests preserved and passing

## Wave 3 — Phase Control Remediation (2026-08-16)

### Completed Fixes
1. **phase-state.json** — Updated from Phase 4 accepted → Phase 5 implementation_complete with full feature/test/warning details
2. **phase-6.json** — Corrected gate naming: "Platform Services" → "Processes and workflow" (per PHASES.md)
3. **phase-gate.ps1** — Expanded from 9 → 12 checks:
   - Added FRONTEND_TESTS (npm test)
   - Added BUNDLE_SIZE (≤250 KB gzipped estimation)
   - Enhanced MIGRATIONS (content verification for all 001-005)
   - Added PHASE_NAME_ALIGNMENT (gate names vs PHASES.md)
4. **ci.yml** — Added:
   - Run ALL migrations (001-005), not just 001-002
   - Frontend unit tests (npm test)
   - Bundle size check (≤250 KB gzipped)
   - Secret scanning step
5. **ACTIVE.md** — Updated to reflect Phase 5 implementation_complete status

### Remaining Before Phase 5 Acceptance
- Integration tests (require Docker/PostgreSQL — CI_PENDING locally, PASS in CI)
- Security review of auth/RBAC paths
- Code review
- Phase gate script execution (requires pwsh — not available on this machine)
- Git commit of all Wave 3 changes

## Completed
- Phase 0: APPROVED (engineering foundation)
- Phase 1: ACCEPTED (2026-08-15)
- Phase 2: ACCEPTED (2026-08-15)
- Phase 3: ACCEPTED (2026-08-15)
- Phase 4: ACCEPTED (2026-08-16)

## Next Phase
Phase 6 — Processes and workflow (pending Phase 5 acceptance)
