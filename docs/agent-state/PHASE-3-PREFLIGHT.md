# Phase 3 — Preflight Report

**Phase:** 3 — UI (Generic Forms, Grids, Lookups, Menus)
**Date:** 2026-08-15
**Status:** PREFLIGHT PASS / IMPLEMENTATION READY: YES / IMPLEMENTATION: NOT STARTED

---

## 1. Phase 2 Baseline

| Check | Result |
|---|---|
| phase-2-accepted tag | EXISTS (e3beb38) |
| Acceptance commit | EXISTS (`Accept Phase 2 - Metadata Runtime`) |
| CI status | GREEN (all tests pass on GitHub Actions) |
| Working tree | CLEAN (no uncommitted changes to production code) |
| phase-state.json | status: accepted, gateStatus: accepted, all checks: pass |
| ACTIVE.md | Phase 3 UNLOCKED |
| Phase 1 accepted | YES |
| Phase 2 tests passing | 240 (160 core + 33 schema + 47 integration) |

**Baseline verified. Phase 3 may proceed after preflight acceptance.**

---

## 2. Phase 3 Scope — Exact Requirements from HLD/LLD

Phase 3 is defined in HLD/LLD Section 34 (Migration Plan) and Section 35 (Implementation Items 21-30):

| # | HLD/LLD Item | Requirement | Backend Impact | Frontend Impact | Database Impact | Security Impact | Phase 2 Dependency | Acceptance Criteria |
|---|---|---|---|---|---|---|---|---|
| 21 | Standardize React/TypeScript | ESLint, Prettier, TypeScript strict mode, code style | None | Config | None | None | None | Linting passes |
| 22 | Define metadata JSON contract | TypeScript interfaces matching HLD Section 9 | Meta API output | Types | None | None | None | Contract matches HLD Section 9 |
| 23 | Implement generic form renderer | Meta API + Data API endpoints | Program.cs routes | GenericForm, FieldRenderer | None | XSS (SEC-P3-004) | POValidator, POLifecycleManager, IValRuleEngine | Form renders from metadata, validates, submits |
| 24 | Implement generic grid | Data API list endpoint | Generic list endpoint with pagination | GenericGrid | None | Tenant isolation (SEC-P3-002) | IMetadataGraph | Grid renders paginated data |
| 25 | Implement list/table/search lookup controls | Lookup API endpoint | /api/lookup/{referenceId} | TableLookup, ListDropdown | None | Rate limiting (SEC-P3-007) | MetadataGraph, ReferenceValueValidator | Lookups return key-value pairs |
| 26 | Implement search popup behavior | Search parameter on Lookup API | Search filtering on lookup | SearchPopup | None | Tenant isolation | MetadataGraph | Search popup filters results |
| 27 | Implement display-logic evaluation | DisplayLogicEvaluator class | Server-side evaluation in Meta API | Client-side evaluation | None | Injection (SEC-P3-010) | None | Display logic evaluates correctly, rejects SQL |
| 28 | Implement field groups/layout metadata | Window metadata includes field groups | Meta API includes fieldGroups | FieldGroup sections | SysFieldGroup table | None | SysWindow migration | Field groups rendered |
| 29 | Implement menu renderer | /api/meta/menu endpoint | Menu API | MenuRenderer | SysMenu table | RBAC | None | Menu renders with hierarchy |
| 30 | Provide custom-form escape hatch | customForm flag in metadata contract | Optional custom form support | Conditional rendering | None | Escalation (SEC-P3-006) | POValidator | Custom form still validates |

---

## 3. Architecture Review

### Component Boundaries

```
frontend/                    -- Phase 3 UI (new)
  metadata/                  -- API contract types, React Query hooks
  forms/                     -- GenericForm, FieldRenderer, control map
  grids/                     -- GenericGrid, pagination, sorting
  lookup/                    -- TableLookup, SearchPopup, ListDropdown
  menus/                     -- MenuRenderer
  api/                       -- Axios instance, error handling

Platform.API/               -- Phase 3 API endpoints (new)
  GenericDataApi.cs          -- /api/data/{table} CRUD
  GenericMetaApi.cs          -- /api/meta/window/{id}, /api/meta/menu
  GenericLookupApi.cs        -- /api/lookup/{referenceId}

Platform.Core/Runtime/      -- Phase 2 (consume, not modify)
  IMetadataGraph             -- MetadataGraph (singleton)
  MetadataCacheService       -- IMemoryCache + Redis
  POValidator               -- mandatory -> type -> length -> reference -> valrule
  POLifecycleManager        -- Create/Update/Delete with hooks/rollback
  IValRuleEngine            -- SQL/Regex evaluation
  ContextVariableResolver   -- $UserId, $TenantId, $OrgId
  TypeValidator             -- varChar, integer, bigint, boolean, DateTime
  ReferenceValueValidator   -- LIST/Table validation
  CacheInvalidationService  -- Redis pub/sub invalidation
```

### Interface Between Phase 2 and Phase 3

| Phase 2 Service | Interface | Phase 3 Consumer |
|---|---|---|
| `MetadataGraph` | `IMetadataGraph` | Meta API (builds JSON contract), Lookup API |
| `POValidator` | (concrete) | Data API (validates POST/PUT payloads) |
| `POLifecycleManager` | (concrete) | Data API (orchestrates CRUD operations) |
| `ValRuleEngine` | `IValRuleEngine` | Data API (ValRule validation during save) |
| `MetadataCacheService` | `IMetadataCache` | Meta API (caches window metadata) |
| `ContextVariableResolver` | `IContextVariableResolver` | Data API (resolves $UserId, $TenantId, etc.) |

### Architectural Ambiguities Requiring ADRs

| ADR | Topic | Urgency | Description |
|---|---|---|---|
| ADR-0005 | React component library | Before impl | Choice affects build effort (Ant Design vs MUI vs unstyled) |
| ADR-0006 | Display logic expression grammar | Before impl | Syntax for conditional field visibility |
| ADR-0007 | Generic list filter DSL | Before impl | Filter query format for grid endpoints |
| ADR-0009 | UI metadata table schema | Before impl | SysWindow, SysTab, SysField, SysFieldGroup, SysMenu DDL |

### Dependency Direction

```
frontend/ --> Platform.API (HTTP/REST)
Platform.API --> Platform.Core (Phase 2 runtime services)
No changes to existing dependency direction
```

### Service Boundaries

- **Generic Data API**: Stateless, consumes POLifecycleManager + POValidator
- **Generic Meta API**: Stateless, consumes IMetadataGraph + MetadataCacheService
- **Generic Lookup API**: Stateless, consumes IMetadataGraph
- **Display Logic Evaluator**: Stateless utility in Platform.Core/Runtime

### API Contracts

**Metadata API — Window:**
```
GET /api/meta/window/{windowId}
Response: 200 OK
{ windowId, name, tabs: [{ table, fields: [{ columnName, label, help, controlType, isMandatory, isReadOnly, displayLogic, valRule }]}] }
Headers: X-Contract-Version: v1
Auth: Required (Phase 4)
```

**Data API — List:**
```
GET /api/data/{table}?page=1&pageSize=20&sortBy=Name&sortOrder=asc&filter.Column=Name&filter.Operator=like&filter.Value=Acme
Response: 200 OK
{ items: [...], totalCount, page, pageSize }
Auth: Required (Phase 4)
```

**Data API — Create:**
```
POST /api/data/{table}
Body: { fieldName1: value1, fieldName2: value2 }
Response: 201 Created
Headers: Location: /api/data/{table}/{id}
Auth: Required (Phase 4)
```

**Lookup API:**
```
GET /api/lookup/{referenceId}?search=term&page=1&pageSize=50
Response: 200 OK
{ referenceName, validationType, items: [{ key, display }], total }
Auth: Required (Phase 4)
```

---

## 4. Backend Review

### What Phase 3 Builds On

| Component | Status | Phase 3 Action |
|---|---|---|
| `MetadataGraph` | EXISTS (Phase 2) | Consume for Meta API + Lookup API |
| `MetaColumn` | EXISTS (Phase 2) | Consume for field rendering |
| `POValidator` | EXISTS (Phase 2) | Consume for Data API validation |
| `POLifecycleManager` | EXISTS (Phase 2) | Consume for Data API CRUD |
| `IValRuleEngine` | EXISTS (Phase 2) | Consume for Data API validation |
| `IContextVariableResolver` | EXISTS (Phase 2) | Consume for Data API context |
| `MetadataCacheService` | EXISTS (Phase 2) | Consume for Meta API caching |
| `CacheInvalidationService` | EXISTS (Phase 2) | No change needed |
| `IPOFactory` | EXISTS (Phase 2) | Not needed for Phase 3 |

### What Phase 3 Creates in Backend

| Component | Files | Purpose |
|---|---|---|
| Generic Data API endpoints | `Program.cs` additions | /api/data CRUD |
| Metadata API endpoints | `Program.cs` additions | /api/meta/window, /api/meta/menu |
| Lookup API endpoints | `Program.cs` additions | /api/lookup/{referenceId} |
| DisplayLogicEvaluator | `Platform.Core/Runtime/DisplayLogicEvaluator.cs` | Boolean expression evaluation |
| MetadataContractBuilder | `Platform.Core/Runtime/MetadataContractBuilder.cs` | Assembles JSON from metadata |
| Window model | `Platform.Core/Metadata/SysWindow.cs` | Window entity |
| Tab model | `Platform.Core/Metadata/SysTab.cs` | Tab entity |
| Field model | `Platform.Core/Metadata/SysField.cs` | Field entity |
| FieldGroup model | `Platform.Core/Metadata/SysFieldGroup.cs` | Field group entity |
| Menu model | `Platform.Core/Metadata/SysMenu.cs` | Menu entity |
| Window repository | `Platform.Data/Repositories/SysWindowRepository.cs` | Dapper queries |
| Tab repository | `Platform.Data/Repositories/SysTabRepository.cs` | Dapper queries |
| Field repository | `Platform.Data/Repositories/SysFieldRepository.cs` | Dapper queries |
| FieldGroup repository | `Platform.Data/Repositories/SysFieldGroupRepository.cs` | Dapper queries |
| Menu repository | `Platform.Data/Repositories/SysMenuRepository.cs` | Dapper queries |

---

## 5. Database Review

### Phase 3 Database Migration: REQUIRED

The following tables are **required** for Phase 3 but do **not** exist in the current database:

| Table | Purpose | HLD Section |
|---|---|---|
| `SysWindow` | Window definitions (name, description, linked table) | Section 8 |
| `SysTab` | Tabs within windows (parent/window, seq, table) | Section 8 |
| `SysField` | Fields within tabs (parent/tab, column, seq, control type) | Section 8 |
| `SysFieldGroup` | Field groupings within tabs (name, seq) | Section 8 |
| `SysMenu` | Menu hierarchy (parent, seq, window/process link) | Section 8 |

**Current state:** `SysTable.SysWindow_ID` exists as a nullable FK that points to nothing. These tables must be designed and migrated before the UI layer can be meaningful.

**Migration strategy:**
- New migration file in `src/Platform.Data/Migrations/` (e.g., `003_Create_UI_Metadata_Schema.sql`)
- Ordered after `002_Seed_Dictionary_Data.sql`
- Include seed data for sample window/tab/field structure
- All identifiers double-quoted to match existing convention
- FK constraints: SysTab -> SysWindow, SysField -> SysTab, SysFieldGroup -> SysTab

**No other database changes required** (indexes, RLS, sequences, trees, audit — all deferred to later phases).

---

## 6. Frontend Architecture

### Structure

```
frontend/src/
  metadata/
    types.ts                  -- TypeScript interfaces for API contracts
    hooks.ts                  -- TanStack Query hooks
  forms/
    GenericForm.tsx            -- Window form renderer
    FieldRenderer.tsx          -- Control type dispatch
    controls/
      TextInput.tsx
      NumberInput.tsx
      DateInput.tsx
      YesNoToggle.tsx
      ListDropdown.tsx
      TableLookup.tsx
      SearchPopup.tsx
      TextArea.tsx
      ImageUpload.tsx
  grids/
    GenericGrid.tsx            -- Table renderer
    Pagination.tsx
    Sorting.tsx
  lookup/
    TableLookup.tsx            -- Search popup for TABLE refs
    SearchPopup.tsx            -- Search for SEARCH refs
    ListDropdown.tsx           -- Dropdown for LIST refs
  menus/
    MenuRenderer.tsx           -- Hierarchical menu
  api/
    client.ts                  -- Axios instance with base URL
  components/
    ErrorBoundary.tsx          -- Global error handling
    LoadingSpinner.tsx         -- Shared loading state
```

### Data Flow

```
React App
  -> TanStack Query fetches /api/meta/window/{id}
  -> MetaContract parsed into TypeScript interfaces
  -> GenericForm renders from MetaContract.tabs[].fields[]
  -> react-hook-form manages form state
  -> On submit: POST/PUT /api/data/{table}
  -> TanStack Query invalidates and refetches
```

### Component Library Recommendation

**Recommendation:** Choose a component library before implementation (ADR-0005). Options:
- **Ant Design** (recommended for enterprise ERP-style UI, matches ADempiere inspiration)
- **MUI** (widely used, good React 19 support)
- **Radix + custom CSS** (accessibility-first, more build effort)
- **Unstyled primitives** (maximum flexibility, highest effort)

---

## 7. UX/UI Review

### Information Architecture

- **Menu-driven navigation**: Primary navigation is the menu from SysMenu
- **Window-centric layout**: Each window = one business entity (e.g., "Library Book")
- **Tab-based organization**: Windows can have multiple tabs for different field groups
- **Field groups**: Fields organized into named sections within tabs

### Navigation

- Menu items link to windows by windowId
- Bread crumbs: Menu -> Window -> (optional tab)
- Responsive: collapsed menu on mobile, hamburger toggle

### Forms

- Label-above layout for desktop, label-inline for same-line fields
- Mandatory fields: asterisk indicator
- Help text below field label
- Validation errors: inline under field, red styling
- Submit button: primary, disabled during loading
- Loading state: spinner on submit button
- Empty state: "No fields configured" message

### Tables/Grids

- Column headers from metadata (label from SysElement)
- Sortable columns (click header to toggle asc/desc)
- Pagination controls at bottom
- Empty state: "No records found"
- Row selection for bulk operations (deferred)
- Responsive: horizontal scroll on mobile

### Accessibility

- All form fields have associated `<label>` elements
- Mandatory field indicators have aria-required
- Error messages have aria-describedby
- Grid has role="grid", row roles, correct tab order
- Keyboard navigation: Tab through fields, Enter to submit
- Color contrast: WCAG AA minimum

### Error States

- 401: Redirect to login (Phase 4)
- 403: "You don't have access to this page"
- 404: "Record not found"
- 422: Field-level validation errors displayed inline
- 500: "Server error — please try again" with retry button

---

## 8. Security Review

### Phase 3 Security Findings Register

Source: `docs/security/PHASE-3-SECURITY-FINDINGS-REGISTER.md` (12 findings)

| # | ID | Severity | Title | Requires Phase 4 |
|---|---|---|---|---|
| 1 | SEC-P3-001 | CRITICAL | No authentication on any endpoint | No |
| 2 | SEC-P3-002 | CRITICAL | Tenant/org isolation missing from CRUD queries | Yes |
| 3 | SEC-P3-003 | CRITICAL | SQL injection via dynamic table/column identifiers | No |
| 4 | SEC-P3-004 | HIGH | XSS via metadata-driven content rendering | No |
| 5 | SEC-P3-005 | HIGH | Overbroad data projection | Yes |
| 6 | SEC-P3-006 | HIGH | Column-level access control not enforced | Yes |
| 7 | SEC-P3-007 | MEDIUM | DoS on high-volume reference tables | No |
| 8 | SEC-P3-008 | MEDIUM | No audit logging for CRUD operations | No |
| 9 | SEC-P3-009 | MEDIUM | CSRF on JWT bearer token | No |
| 10 | SEC-P3-010 | MEDIUM | Client-side display-logic bypass | No |
| 11 | SEC-P3-011 | MEDIUM | Row-level predicate not enforced in bulk ops | Yes |
| 12 | SEC-P3-012 | LOW | Swagger exposes API without auth | No |

### Defense-in-Depth Layers

| Layer | Mechanism | Finding |
|---|---|---|
| 1 | Authentication (JWT) | SEC-P3-001 |
| 2 | Authorization (RBAC) | SEC-P3-002, 005, 006 |
| 3 | Tenant/Predicate Injection | SEC-P3-002 |
| 4 | Identifier Validation | SEC-P3-003 |
| 5 | XSS Prevention | SEC-P3-004 |
| 6 | Rate Limiting | SEC-P3-007 |
| 7 | Audit Logging | SEC-P3-008 |
| 8 | CSRF Protection | SEC-P3-009 |

---

## 9. Test Strategy

Source: `docs/testing/PHASE-3-TEST-MATRIX.md`

| Category | Count | Description |
|---|---|---|
| Unit Tests - Backend | 52 | Display logic, window builder, query builder, contract |
| Unit Tests - Frontend | 45 | Form/grid/lookup components, hooks |
| Integration Tests - API | 30 | Window meta, CRUD, lookup, menu, escape hatch |
| Integration Tests - React | 20 | Form/grid/lookup with mocked API |
| E2E Tests | 12 | Full CRUD, validation, lookup, display logic, menu |
| API Contract Tests | 25 | JSON schema validation for all endpoints |
| Security Tests | 18 | Auth, tenant isolation, XSS, SQL injection, escape hatch |
| Performance Tests | 8 | Grid 1000 rows, 100k rows, API latency |
| **Total new tests** | **210** | |
| Regression tests | 240 | All Phase 0-2 tests must still pass |

### Test Categories Mapping

| HLD/LLD Item | Test Coverage |
|---|---|
| 22. Metadata JSON contract | PU-CT, PC-MET |
| 23. Generic form renderer | PU-WIN, FU-WR, FU-FF, FU-FM, PI-WIN, PI-CRD, CI-FORM, EE-CRD, EE-VAL |
| 24. Generic grid | PU-QB, PU-QS, FU-GR, PI-CRD, CI-GRID, EE-CRD, PF-GRD |
| 25. Lookup controls | PU-LK, FU-LK, PI-LKP, CI-LKP, EE-LKP |
| 26. Search popup | FU-LK, PI-LKP, CI-LKP, EE-LKP |
| 27. Display logic | PU-DL, FU-FF, CI-FORM, EE-DL, PS-XSS |
| 28. Field groups | PU-FG, FU-WR, PI-FG, CI-FORM |
| 29. Menu renderer | FU-MNU, PI-MNU, EE-MNU |
| 30. Custom form escape hatch | PU-ESC, PI-ESC, CI-FORM, EE-ESC, PS-ESC |

---

## 10. Agent Plan

| Agent | Role | When | Deliverables |
|---|---|---|---|
| `architect` | Review, resolve ambiguity | Before impl | ADRs for component library, display logic grammar |
| `database-engineer` | Design UI metadata tables | Before impl | Migration DDL, seed data |
| `backend-developer` | Build API endpoints + services | During impl | Data API, Meta API, Lookup API, evaluators |
| `frontend-developer` | Build React components | During impl | GenericForm, GenericGrid, Lookup controls, Menu |
| `qa-engineer` | Review test coverage | Before impl | Test matrix (DONE), During impl: write tests |
| `security-reviewer` | Review auth, isolation, injection | Before impl | Security findings register (DONE), During impl: verify mitigations |
| `code-reviewer` | Final review | After impl | Code quality, architecture compliance |
| `ux-reviewer` | Review UI/UX quality | After impl | Accessibility, responsiveness, error states |
| `release-manager` | Validate readiness | After impl | Release checklist, CI verification |
| `phase-gate` | Independent gate check | Final | Gate result |

### Execution Order

1. ADRs (architect) + DB schema (database-engineer)
2. Backend API + services (backend-developer)
3. Frontend components (frontend-developer)
4. Tests (qa-engineer) — parallel with 2 & 3
5. UX review (ux-reviewer)
6. Security review (security-reviewer)
7. Code review (code-reviewer)
8. Phase gate (phase-gate)

---

## 11. Skills Plan

| Skill | Purpose | When | Mandatory |
|---|---|---|---|
| `/commit` | Commit changes | After each logical batch | Mandatory |
| `/code-review` | Final review | After implementation | Mandatory |
| `/security-review` | Security review | After backend impl | Mandatory |
| `/ux-review` | UX review | After frontend impl | Recommended |
| `/release` | Release validation | After all reviews | Mandatory |

---

## 12. Hooks / Control Plane Verification

| Hook | Status | Phase 3 Impact |
|---|---|---|
| `session-start` | WORKING | No change needed |
| `post-edit` | WORKING | No change needed |
| `stop-check` | WORKING | No change needed |
| `precompact` | WORKING | No change needed |
| `postcompact` | WORKING | No change needed |
| `dangerous-command` | WORKING | Prevents destructive DB ops during impl |
| `phase-state.json` | WORKING | Phase 3 unlocked |
| `ACTIVE.md` | WORKING | Current phase tracking |
| `secret-detection` | WORKING | .gitignore covers secrets |
| `hld-gate` | WORKING | Ensures HLD compliance |
| `phase-transition` | WORKING | Requires Phase 2 acceptance (done) |

**All hooks verified. No changes needed for Phase 3.**

---

## 13. Memory / Compaction Strategy

### Before Compaction — Preserve

- Current phase: 3
- Current task: UI implementation
- ADRs created: ADR-0005, ADR-0006, ADR-0007, ADR-0009
- Database schema: UI metadata tables DDL
- API contract shapes: exact JSON response formats
- Implementation order: ordered steps with dependencies
- Failing tests: any test failures during impl
- Decisions: component library choice, display logic grammar
- Unresolved findings: SEC-P3-002, SEC-P3-005, SEC-P3-006 (Phase 4 delegation)
- Next action: implementation step 1

### After Compaction — Verify

- phase-state.json: currentPhase = 3, status = in-progress
- ACTIVE.md: current task, completed steps
- All ADRs present in docs/adr/
- Test matrix still accurate
- No unresolved blockers

---

## 14. Architecture Review Summary

### Component Boundaries

| Layer | Responsibility | Phase 3 Code |
|---|---|---|
| Frontend | UI rendering, user interaction | GenericForm, GenericGrid, Lookup, Menu |
| Platform.API | HTTP endpoints | Data/Meta/Lookup API routes |
| Platform.Core | Business logic, validation | DisplayLogicEvaluator, MetadataContractBuilder |
| Platform.Data | Data access | Repository classes for UI metadata tables |
| Platform.Metadata | PO factory | No changes needed |
| PostgreSQL | Persistence | UI metadata tables (SysWindow, SysTab, etc.) |

### Concurrency Considerations

- Generic Data API must handle concurrent updates to same record
- POLifecycleManager already supports optimistic concurrency (deferred)
- Metadata cache invalidation: UI metadata changes -> invalidate cache
- Redis pub/sub: same pattern as Phase 2, no changes needed

### Error Handling

- 400: Invalid table/column name, malformed request
- 401: No auth token (Phase 4)
- 403: Insufficient permissions (Phase 4)
- 404: Unknown table, unknown record, unknown window
- 422: Validation errors (mandatory, type, length, reference, valrule)
- 500: Unexpected server error

---

## 15. ADRs Required

| ADR | Topic | Priority |
|---|---|---|
| ADR-0005 | React component library | High — blocks frontend impl |
| ADR-0006 | Display logic expression grammar | High — blocks backend impl |
| ADR-0007 | Generic list filter DSL | Medium — can evolve during impl |
| ADR-0009 | UI metadata table schema | High — blocks DB migration |

---

## 16. Implementation Order

### Step 1: ADRs + DB Schema (PREREQUISITE)
- **Agent:** architect, database-engineer
- **Files:** docs/adr/0005-*.md, 0006-*.md, 0007-*.md, 0009-*.md
- **Database:** Migration 003_Create_UI_Metadata_Schema.sql
- **Tests:** Schema contract tests for new tables
- **Acceptance:** ADRs written, migration DDL reviewed

### Step 2: Backend Model Classes + Repositories
- **Agent:** backend-developer, database-engineer
- **Files:** Platform.Core/Metadata/SysWindow.cs, SysTab.cs, SysField.cs, SysFieldGroup.cs, SysMenu.cs
- **Files:** Platform.Data/Repositories/SysWindowRepository.cs, etc.
- **Tests:** Schema contract tests
- **Acceptance:** Repositories load from seeded data

### Step 3: Backend Services
- **Agent:** backend-developer
- **Files:** DisplayLogicEvaluator.cs, MetadataContractBuilder.cs
- **Tests:** Unit tests (PU-WIN, PU-FG, PU-DL, PU-LK, PU-CT, PU-QB, PU-QS)
- **Acceptance:** All unit tests pass

### Step 4: Backend API Endpoints
- **Agent:** backend-developer
- **Files:** Program.cs additions (Generic Data API, Meta API, Lookup API)
- **Tests:** Integration tests (PI-WIN, PI-CRD, PI-LKP, PI-FG, PI-MNU, PI-ESC)
- **Acceptance:** All API endpoints return correct status codes and data

### Step 5: Frontend Types + API Client
- **Agent:** frontend-developer
- **Files:** frontend/src/metadata/types.ts, hooks.ts, client.ts
- **Tests:** Contract tests (PC-MET, PC-DTA, PC-LKP)
- **Acceptance:** TypeScript compiles with types matching API responses

### Step 6: Frontend Components
- **Agent:** frontend-developer
- **Files:** GenericForm.tsx, GenericGrid.tsx, Lookup controls, MenuRenderer
- **Tests:** Unit tests (FU-WR, FU-FF, FU-FM, FU-GR, FU-LK)
- **Acceptance:** Components render from metadata, handle all states

### Step 7: Integration Tests + E2E Tests
- **Agent:** qa-engineer
- **Files:** CI-FORM, CI-GRID, CI-LKP, EE-*, playwright/
- **Tests:** Integration tests (React components with mocked API), E2E tests
- **Acceptance:** All integration + E2E tests pass

### Step 8: Security Tests + Performance Tests
- **Agent:** qa-engineer
- **Files:** PS-* tests, PF-* tests
- **Tests:** Auth, tenant isolation, XSS, SQL injection, performance
- **Acceptance:** All security tests pass, performance within bounds

### Step 9: Reviews
- **Agent:** ux-reviewer, security-reviewer, code-reviewer, release-manager
- **Acceptance:** All reviews pass with P0=0, blocking P1=0

### Step 10: Phase Gate
- **Agent:** phase-gate, orchestrator
- **Acceptance:** Gate PASS, CI GREEN

---

## 17. Acceptance Criteria

Phase 3 is considered complete when ALL of the following are true:

1. **Build:** `dotnet build Platform.sln` — 0 warnings, 0 errors
2. **Unit Tests:** All 210 new unit/integration/E2E/contract/security/perf tests pass
3. **Regression:** All 240 Phase 0-2 tests still pass
4. **Database:** Migration 003 applies cleanly, seed data correct, schema contract tests pass
5. **API:** All endpoints return correct status codes, shapes, headers
6. **Security:** All 12 security findings mitigated or properly deferred
7. **Frontend:** `npm test` passes, `npm run build` succeeds
8. **UX:** Accessible (WCAG AA), responsive, proper error/loading/empty states
9. **HLD Compliance:** All Section 35 items 21-30 implemented
10. **Acceptance Criteria:** All 8 criteria from HLD Section 36 (AC-6, AC-7, AC-8, etc.)
11. **Code Review:** Architecture compliance, no drift from HLD
12. **CI:** GitHub Actions GREEN (backend + frontend + tests)
13. **Git:** Clean working tree, meaningful commits
14. **Phase State:** phase-state.json updated to accepted

---

## 18. Risks

| # | Risk | Impact | Mitigation |
|---|---|---|---|
| 1 | UI metadata table schema design is open | Blocks entire phase | ADR-0009 + database-engineer review before impl |
| 2 | Component library choice affects effort significantly | Schedule impact | Decide early via ADR-0005 |
| 3 | Display logic grammar ambiguity | Blocks backend service | ADR-0006 + simple boolean expressions first |
| 4 | Phase 4 auth must be layered on top without refactoring | Technical debt risk | API code accepts IReadOnlyContext from start |
| 5 | Frontend test suite needs new dependencies | Build complexity | Pre-add to package.json (axios-mock-adapter, @testing-library/user-event) |
| 6 | E2E tests require full platform running | CI complexity | Separate CI job with API + frontend service |
| 7 | Tenant isolation (SEC-P3-002) requires QueryBuilder (Phase 4) | Incomplete isolation | Data API accepts context; QueryBuilder deferred |
| 8 | Large number of tests (210) | Time to write | Parallel agents, template-driven test creation |

---

## 19. Open Questions

| # | Question | Impact | Resolved By |
|---|---|---|---|
| 1 | Which React component library? | Frontend effort, API design | ADR-0005 |
| 2 | Display logic expression syntax? | Backend evaluator design | ADR-0006 |
| 3 | Filter DSL format for grid queries? | Data API contract | ADR-0007 |
| 4 | UI metadata table DDL schema? | Database migration | ADR-0009 |
| 5 | Should Swagger be versioned for future API evolution? | API design | Decision: no versioning for Phase 3, path-based v1 |
| 6 | Should the custom form escape hatch (item 30) be implemented in Phase 3 or deferred? | Scope | Decision: minimal implementation (flag + fallback), full implementation deferred |

---

## Preflight Exit Conditions Checklist

| Condition | Status |
|---|---|
| Scope completely identified | PASS |
| HLD requirements mapped (Items 21-30) | PASS |
| Architecture reviewed | PASS |
| Backend reviewed | PASS |
| Database impact known | PASS (5 tables required) |
| API contracts defined | PASS |
| Frontend scope defined | PASS |
| UX reviewed | PASS |
| Security reviewed | PASS (12 findings) |
| Test matrix created | PASS (210 tests) |
| Agent plan created | PASS |
| Skill plan created | PASS |
| Hooks/control-plane verified | PASS |
| Implementation order defined | PASS (10 steps) |
| Acceptance criteria defined | PASS (14 criteria) |
| ADRs identified | PASS (4 ADRs) |

---

## PREFLIGHT REPORT SUMMARY

```
PHASE 3 PREFLIGHT REPORT

Phase 2 baseline:
  Tag: phase-2-accepted (e3beb38)
  CI: GREEN
  Tests: 240 passing
  phase-state.json: accepted

Phase 3 scope:
  10 requirements (HLD/LLD Items 21-30)
  Backend: 5 new API endpoint groups, 2 new services, 5 new models, 5 new repos
  Database: 5 new tables (SysWindow, SysTab, SysField, SysFieldGroup, SysMenu)
  Frontend: 8+ components (GenericForm, GenericGrid, Lookup controls, Menu, etc.)
  Tests: 210 new (52 unit backend, 45 unit frontend, 30 integration API, 20 integration React, 12 E2E, 25 contract, 18 security, 8 performance)
  ADRs: 4 required (component library, display logic, filter DSL, UI schema)
  Security: 12 findings (3 CRITICAL, 3 HIGH, 5 MEDIUM, 1 LOW)

Backend:
  - Generic Data API (5 endpoints: GET list, GET single, POST, PUT, DELETE)
  - Metadata API (3 endpoints: GET window, GET tables, GET menu)
  - Lookup API (1 endpoint: GET reference with search/pagination)
  - DisplayLogicEvaluator (boolean expression evaluator)
  - MetadataContractBuilder (assembles JSON from metadata)

Database:
  REQUIRED MIGRATION — 5 new tables
  SysWindow, SysTab, SysField, SysFieldGroup, SysMenu
  Seed data for sample structure
  FK chain: SysTab -> SysWindow, SysField -> SysTab, SysFieldGroup -> SysTab

API:
  GET /api/meta/window/{windowId} -> WindowMetadataContract
  GET /api/data/{table}?page=&pageSize=&sortBy=&filter.=&filter.=&filter.= -> PaginatedList
  POST /api/data/{table} -> CreatedRecord | ValidationErrors
  PUT /api/data/{table}/{id} -> UpdatedRecord | ValidationErrors
  DELETE /api/data/{table}/{id} -> 204 No Content
  GET /api/lookup/{referenceId}?search=&page=&pageSize= -> LookupResponse

Frontend:
  metadata/types.ts — TypeScript interfaces for all API contracts
  GenericForm — renders from metadata, react-hook-form integration
  GenericGrid — paginated, sortable table from metadata
  Lookup controls — ListDropdown, TableLookup, SearchPopup
  MenuRenderer — hierarchical menu from metadata
  Display logic evaluation — client-side for visibility, server-side for security

UX/UI:
  Label-above forms with mandatory indicators
  Sortable tables with pagination
  Accessible (WCAG AA), responsive
  Proper error/loading/empty states
  Keyboard navigation support

Security:
  12 findings identified and documented
  3 CRITICAL: Auth, tenant isolation, SQL injection
  3 HIGH: XSS, data projection, column access
  5 MEDIUM: DoS, audit, CSRF, display-logic bypass, row-level
  1 LOW: Swagger exposure
  Defense-in-depth: 8 layers defined

Testing:
  210 new tests (requirement-driven, not arbitrary)
  240 regression tests (must all still pass)
  CI: extended with frontend tests, E2E, contract validation

Agents:
  architect, database-engineer, backend-developer, frontend-developer,
  qa-engineer, security-reviewer, ux-reviewer, code-reviewer,
  release-manager, phase-gate

Skills:
  /commit, /code-review, /security-review, /ux-review, /release

Hooks:
  All existing hooks verified, no changes needed

Memory/Compaction:
  Pre-compact: save phase, ADRs, schema, decisions, test coverage
  Post-compact: verify phase-state.json, ACTIVE.md, test matrix

Architecture:
  Layered: frontend -> Platform.API -> Platform.Core -> Platform.Data -> PostgreSQL
  No changes to existing dependency direction
  Phase 4 auth must be layerable without refactoring

ADRs:
  4 required: ADR-0005 (component library), ADR-0006 (display logic),
  ADR-0007 (filter DSL), ADR-0009 (UI schema)

Implementation order:
  1. ADRs + DB schema
  2. Model classes + repositories
  3. Backend services
  4. API endpoints
  5. Frontend types + client
  6. Frontend components
  7. Integration + E2E tests
  8. Security + performance tests
  9. Reviews (UX, security, code)
  10. Phase gate

Acceptance criteria:
  14 specific criteria covering build, tests, database, API, security,
  frontend, UX, HLD compliance, code review, CI, git, phase state

Risks:
  8 risks identified with mitigations
  Highest: UI metadata table schema design (blocks entire phase)
  Second: Component library choice (affects effort)

Open questions:
  6 open questions, 4 require ADRs, 2 are design decisions
```

---

## PREFLIGHT STATUS

**PASS**

## IMPLEMENTATION READY

**YES**

## PHASE 3 IMPLEMENTATION

**NOT STARTED**

---

*End of Preflight Report. Implementation NOT started. Awaiting authorization.*
