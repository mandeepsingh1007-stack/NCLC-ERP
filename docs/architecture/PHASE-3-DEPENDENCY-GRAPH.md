# Phase 3 — Architecture Dependency Graph

**Phase:** 3 — UI (Generic Forms, Grids, Lookups, Menus)
**Date:** 2026-08-15
**Status:** Design Closure

---

## 1. Layer Dependency Graph

```
┌─────────────────────────────────────────────────────────┐
│                    PHASE 3 — FRONTEND                    │
│  frontend/                                               │
│  ├── metadata/        (TypeScript interfaces)            │
│  ├── forms/           (GenericForm, FieldRenderer)       │
│  ├── grids/           (GenericGrid, Pagination)          │
│  ├── lookup/          (TableLookup, SearchPopup)         │
│  ├── menus/           (MenuRenderer)                     │
│  └── api/             (Axios instance, hooks)            │
└────────────────────────┬────────────────────────────────┘
                         │ HTTP/REST (JSON)
                         ▼
┌─────────────────────────────────────────────────────────┐
│                 PHASE 3 — API LAYER                      │
│  Platform.API/                                           │
│  ├── GenericDataApi.cs    (/api/data/{table})           │
│  ├── GenericMetaApi.cs    (/api/meta/window/{id})       │
│  └── GenericLookupApi.cs  (/api/lookup/{refId})         │
└────────────────────────┬────────────────────────────────┘
                         │ DI (interfaces)
                         ▼
┌─────────────────────────────────────────────────────────┐
│           PHASE 2 — APPLICATION / DOMAIN RUNTIME        │
│  Platform.Core/Runtime/                                  │
│  ├── MetadataGraph          (IMetadataGraph)             │
│  ├── MetadataCacheService   (IMetadataCache)             │
│  ├── CacheInvalidationService (Redis pub/sub)           │
│  ├── POValidator            (mandatory → type → ...)     │
│  ├── POLifecycleManager     (Create/Update/Delete)       │
│  ├── ValRuleEngine          (SQL/regex validation)      │
│  ├── ContextVariableResolver ($UserId, $TenantId)       │
│  ├── TypeValidator          (varChar, integer, ...)     │
│  └── ReferenceValueValidator (LIST/Table validation)    │
└────────────────────────┬────────────────────────────────┘
                         │ DI (interfaces)
                         ▼
┌─────────────────────────────────────────────────────────┐
│              PHASE 1 — DATA ACCESS LAYER                │
│  Platform.Data/Repositories/                             │
│  ├── SysElementRepository     (Dapper)                  │
│  ├── SysTranslationRepository                             │
│  ├── SysReferenceRepository                               │
│  ├── SysReferenceListRepository                           │
│  ├── SysReferenceTableRepository                          │
│  ├── SysTableRepository                                   │
│  ├── SysColumnRepository                                  │
│  └── SysValRuleRepository                                 │
└────────────────────────┬────────────────────────────────┘
                         │ Npgsql connection
                         ▼
┌─────────────────────────────────────────────────────────┐
│                  PHASE 1 — POSTGRESQL                   │
│  Database Schema:                                        │
│  ├── sys_element, sys_element_trl                       │
│  ├── sys_reference, sys_reference_list,                   │
│  │     sys_reference_table                               │
│  ├── sys_table, sys_column                              │
│  └── sys_val_rule                                       │
│                                                          │
│  PHASE 3 (NEW):                                          │
│  ├── sys_window                                           │
│  ├── sys_tab                                              │
│  ├── sys_field                                            │
│  ├── sys_field_group                                      │
│  └── sys_menu                                             │
└─────────────────────────────────────────────────────────┘
```

---

## 2. Metadata Cache Dependency Graph

```
┌─────────────────────────────────────────┐
│          FRONTEND (TanStack Query)       │
│  react-query client-side cache           │
│  - Window metadata: 5 min TTL           │
│  - Menu data: 5 min TTL                 │
│  - Lookup data: 5 min TTL               │
│  - Data records: no cache (volat ile)    │
└──────────────┬──────────────────────────┘
               │ React Query invalidation
               ▼
┌─────────────────────────────────────────┐
│    CACHE INVALIDATION SERVICE (Redis)   │
│  - Subscribes to DictionaryChanged channel│
│  - Publishes invalidation on metadata change│
│  - Reconnect/resubscribe on disconnect   │
└──────────────┬──────────────────────────┘
               │ Redis pub/sub
               ▼
┌─────────────────────────────────────────┐
│      IMEMORYCACHE (per-node, local)     │
│  - Window metadata                      │
│  - Menu hierarchy                       │
│  - Table/column metadata                │
│  - Lookup reference data                │
│  - TTL: 5 min for metadata, 1 min for data│
└──────────────┬──────────────────────────┘
               │ On cache miss
               ▼
┌─────────────────────────────────────────┐
│      DATA ACCESS LAYER (Dapper)         │
│  - Reads from PostgreSQL                │
│  - Populates IMemoryCache on miss       │
│  - Reads from PostgreSQL                │
└─────────────────────────────────────────┘
```

---

## 3. API Request Flow

### 3.1 GET /api/meta/window/{id}

```
HTTP Request
    ↓
[Phase 4: Auth middleware]  ← NOT in Phase 3
    ↓
IReadOnlyContext = null context (Phase 3)
    ↓
Check IMemoryCache["window:{id}"]
    ↓
Cache HIT → return cached metadata
    ↓
Cache MISS → MetadataGraph.BuildWindowMetadata(windowId)
    ↓
  → Load sys_window from DB
  → Load sys_tab (FK to sys_window)
  → Load sys_field (FK to sys_tab)
  → Load sys_field_group (FK to sys_tab)
  → Load sys_column (FK to sys_field)
  → Load sys_element (FK to sys_column)
  → Load sys_reference (FK to sys_column)
  → Assemble JSON contract
    ↓
Store in IMemoryCache["window:{id}"]
    ↓
Return JSON response
```

### 3.2 GET /api/data/{table}

```
HTTP Request
    ↓
[Phase 4: Auth middleware]  ← NOT in Phase 3
    ↓
IReadOnlyContext = null context (Phase 3)
    ↓
Validate table name against MetadataGraph.GetTableNames()
    ↓
    → NOT in allowlist → 400
    ↓
Validate sortBy column against SysColumn metadata
    ↓
    → NOT in allowlist → 400
    ↓
Parse filter parameter (JSON string → AST)
    ↓
    → Parse error → 400
    ↓
QueryBuilder.BuildSelect(table, columns, filter, sort, pagination, context)
    ↓
    → Validate all field names against SysColumn
    → Inject tenant predicate (null in Phase 3)
    → Inject org predicate (null in Phase 3)
    → Generate parameterized SQL + NpgsqlParameter[]
    ↓
Execute via Dapper
    ↓
Return paginated result
```

### 3.3 POST /api/data/{table}

```
HTTP Request (JSON body)
    ↓
[Phase 4: Auth middleware]  ← NOT in Phase 3
    ↓
IReadOnlyContext = null context (Phase 3)
    ↓
Validate table name
    ↓
Parse JSON body → dynamic object
    ↓
Remove excluded columns (internal, audit)
    ↓
Validate column names against SysColumn
    ↓
    → Unknown column → 400
    ↓
POLifecycleManager.Create(table, values, context)
    ↓
    → POFactory.Create(table)
    → POValidator.Validate(values, columns, context)
    →     → Mandatory check
    →     → Type check (TypeValidator)
    →     → Length check (StringLengthValidator, etc.)
    →     → Reference check (ReferenceValueValidator)
    →     → ValRule check (ValRuleEngine)
    → Set audit fields (CreatedBy, CreatedAt)
    → Parameterized INSERT
    → Audit log (Phase 6)
    → After-save hooks
    ↓
Return created record
```

---

## 4. Frontend Data Flow

```
User action (click Save)
    ↓
react-hook-form: form.handleSubmit(onSubmit)
    ↓
onSubmit(payload)
    ↓
TanStack Query: mutation.mutate(payload)
    ↓
axios POST /api/data/{table}
    ↓
Header: Authorization: Bearer <token> (Phase 4)
Body: { field1: value1, field2: value2, ... }
    ↓
Response: 201 Created → { id: 1, ... }
    ↓
TanStack Query: invalidateQueries(['data', table])
    ↓
React re-render: grid updates
    ↓
Toast notification: "Record saved"
```

---

## 5. No Circular Dependencies

### Verified: No circular dependencies exist

| Layer | Depends On | NOT Depends On |
|---|---|---|
| Frontend | Platform.API (HTTP), TanStack Query, react-hook-form | Phase 2, Phase 1, Database |
| Platform.API | Platform.Core (runtime services), Platform.Data (repositories) | Frontend |
| Platform.Core | Platform.Data (repositories for metadata loading) | Frontend, Platform.API |
| Platform.Data | Npgsql, Dapper | Any application layer |
| PostgreSQL | — | Any application layer |

### Explicit no-go rules:

1. **Frontend NEVER depends on backend code** — only HTTP API
2. **Platform.API NEVER depends on Frontend** — API is backend-only
3. **Platform.Core NEVER depends on Platform.API** — runtime services are infrastructure-independent
4. **Platform.Data NEVER depends on Platform.Core** — data access is lower than domain logic
5. **No cross-phase direct references** — Phase 3 API consumes Phase 2 services via interfaces, not concrete classes

---

## 6. Cross-Phase Dependencies

| Phase 3 Component | Depends On | Phase | Notes |
|---|---|---|---|
| GenericDataApi | MetadataGraph | Phase 2 | Reads metadata for table/column validation |
| GenericDataApi | POValidator | Phase 2 | Validation during POST/PUT |
| GenericDataApi | POLifecycleManager | Phase 2 | CRUD orchestration |
| GenericDataApi | ValRuleEngine | Phase 2 | ValRule evaluation |
| GenericDataApi | ContextVariableResolver | Phase 2 | Resolves $UserId, etc. |
| GenericMetaApi | MetadataGraph | Phase 2 | Builds window JSON contract |
| GenericLookupApi | MetadataGraph | Phase 2 | Resolves reference data |
| GenericLookupApi | ReferenceValueValidator | Phase 2 | Reference validation |
| GenericForm (React) | Metadata contract | Phase 3 (self) | TypeScript interfaces in same phase |
| GenericGrid (React) | Metadata contract | Phase 3 (self) | TypeScript interfaces in same phase |
| DisplayLogicEvaluator | ContextVariableResolver | Phase 2 | Resolves context variables in expressions |
| MetadataCacheService | CacheInvalidationService | Phase 2 | Redis pub/sub invalidation |
| CacheInvalidationService | Redis | Phase 2 (infrastructure) | StackExchange.Redis |

---

## 7. Phase 4 Dependencies (Future)

Phase 3 builds the plumbing for Phase 4:

| Phase 3 Build | Phase 4 Consumes |
|---|---|
| IReadOnlyContext in API endpoints | Auth middleware creates context from JWT |
| TenantPredicate/OrgPredicate in context | Context populated from JWT claims |
| Column allowlist validation | Extended with role-based column access |
| API error handling for 401/403 | Auth middleware returns 401/403 |
| Permission props on UI components | Real permission values from auth |
| Audit call points in Data API | SysChangeLog writes in Phase 6 |

No blocking — Phase 3 is fully functional without Phase 4.

---

## 8. Infrastructure Dependencies

| Component | Infrastructure | Configuration |
|---|---|---|
| MetadataCacheService | Redis | Redis:ConnectionString |
| CacheInvalidationService | Redis pub/sub | Redis:ConnectionString |
| POLifecycleManager | PostgreSQL | ConnectionString: Default |
| ValRuleEngine | PostgreSQL | ConnectionString: Default |
| Dapper repositories | PostgreSQL | ConnectionString: Default |
| Hangfire | PostgreSQL | ConnectionString: Default |
| Serilog | File system + Console | Configuration |
