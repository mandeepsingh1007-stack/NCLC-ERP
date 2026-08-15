# Phase 2 — Metadata Runtime: Test Matrix

**Phase:** 2 (Metadata Runtime)
**Status:** Design
**Based on:** HLD/LLD sections 11 (Metadata Validation Pipeline), 25 (Metadata Cache), 26 (Create Flow), 27 (Update Flow), 28 (Delete Flow), 29 (Dictionary Change Flow), 35 (Implementation Items 11-19)

## Test Summary

| Category | Count | Notes |
|---|---|---|
| Unit Tests | 45 | Pure logic, no DB dependency |
| Integration Tests | 24 | PostgreSQL + Redis containers |
| Regression Tests | 67 | All Phase 1 tests must still pass |
| **Total** | **136** | |

---

## 1. Unit Tests (45)

### 1.1 Metadata Graph (5 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 1 | PU-GRAPH-001 | Unit | Graph construction from SysTable/SysColumn metadata creates correct node set | SysTable and SysColumn model objects available | Graph contains one node per active SysTable; column nodes linked to parent table node | 3 SysTables, 7 SysColumns |
| 2 | PU-GRAPH-002 | Unit | Node relationships are correctly established via SysTableId foreign key | Graph constructor receives list of SysTable + SysColumn | Each column node's parent reference matches its SysTable; graph traversal from table yields correct columns | SysColumn with SysTableId=1, SysTable with SysTableId=1 |
| 3 | PU-GRAPH-003 | Unit | Dependency resolution returns tables in valid topological order | Graph with circular-reference-free metadata | Topological sort returns ordering where parent tables precede child tables | A with FK to B, B with no FK; order: B, A |
| 4 | PU-GRAPH-004 | Unit | Circular dependency detection throws descriptive exception | Graph with circular reference (A->B->A) | `InvalidOperationException` thrown during resolution with cycle path in message | A references B, B references A |
| 5 | PU-GRAPH-005 | Unit | Inactive nodes are excluded from graph construction | Mix of active/inactive SysTables | Only active tables appear in resolved graph; inactive tables silently skipped | 2 active, 1 inactive table |

### 1.2 Metadata Loading (4 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 6 | PU-LOAD-001 | Unit | LoadAll retrieves every active entity from repositories and assembles into metadata snapshot | Mock `ISysRepository<T>` for each entity type | Snapshot contains all entities returned by mocks; count matches total across repos | 10 tables, 30 columns, 5 references |
| 7 | PU-LOAD-002 | Unit | LoadByTable filters to a single table's metadata | Repository mock returning data for multiple tables | Only metadata for requested table returned; SysColumn filtered to matching SysTableId | 3 tables, request "Users" |
| 8 | PU-LOAD-003 | Unit | LoadInactive returns inactive entities when explicitly requested | Repository mock with inactive entries | Inactive entities are returned alongside active ones | 1 inactive column |
| 9 | PU-LOAD-004 | Unit | Load returns empty collection when no entities exist | Repository mock returning empty enumerable | Empty enumerable returned; no null reference exceptions | Empty repos |

### 1.3 IMemoryCache (8 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 10 | PU-CACHE-001 | Unit | Write to IMemoryCache and read back succeeds | IMemoryCache instance (Microsoft.Extensions.Caching.Memory) | Cache.Set followed by Cache.TryGetValue returns true with correct value | KeyValue: "meta:table:1", Value: SysTable |
| 11 | PU-CACHE-002 | Unit | Read from IMemoryCache returns miss when key absent | Fresh IMemoryCache instance | Cache.TryGetValue returns false; no exception | Key "meta:table:999" |
| 12 | PU-CACHE-003 | Unit | Delete removes entry from IMemoryCache | Entry written to cache | Cache.TryGetValue after Remove returns false | Key "meta:table:1" |
| 13 | PU-CACHE-004 | Unit | TTL expiration causes cache entry to become stale | IMemoryCache with SlidingExpiration set to 1ms | After waiting 50ms, Cache.TryGetValue returns false | Key "meta:table:1", TTL=1ms |
| 14 | PU-CACHE-005 | Unit | Concurrent writes to IMemoryCache do not throw | Thread pool with 10 parallel threads writing to same cache | No `InvalidOperationException` or `NullReferenceException`; last writer wins | 10 threads, same key |
| 15 | PU-CACHE-006 | Unit | Concurrent reads during write are safe | One writer thread, 5 reader threads | All reads return either the old value or the new value; no exception | Same key, concurrent access |
| 16 | PU-CACHE-007 | Unit | Cache entries with null values are handled | Cache.Set with null value | Throws `ArgumentNullException` or stores null per IMemoryCache contract — documented behavior tested | Key "meta:table:0", Value = null |
| 17 | PU-CACHE-008 | Unit | Absolute expiration takes precedence over sliding expiration | Cache with both AbsoluteExpiration and SlidingExpiration | Entry expires at absolute time even if accessed within sliding window | AbsoluteExpiration=100ms, SlidingExpiration=1h |

### 1.4 Redis Distributed Cache (5 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 18 | PU-REDIS-001 | Unit | Write to Redis distributed cache succeeds | Redis container running (Testcontainers.StackExchangeRedis) | CacheEntry.Set followed by Get returns correct serialized value | KeyValue: "dict:invalidation", Value: "v1" |
| 19 | PU-REDIS-002 | Unit | Redis read returns miss when key absent | Fresh Redis connection | CacheEntry.Get returns null/miss; no exception | Key "dict:nonexistent" |
| 20 | PU-REDIS-003 | Unit | Redis write failure falls back to local cache only | Redis container stopped; DistributedCache configured with fallback | Local IMemoryCache still works; DistributedCache exceptions caught and logged; no application crash | No Redis available |
| 21 | PU-REDIS-004 | Unit | Redis distributed invalidation message is published | Redis running with pub/sub channel configured | `DictionaryChangedEvent` published to Redis channel; subscribers can consume it | Channel "metadata:invalidation", event version "v2" |
| 22 | PU-REDIS-005 | Unit | Redis distributed invalidation propagates to subscribers | Two cache instances, same Redis, same channel | Instance B receives invalidation event from instance A's publish | Instance A invalidates table 1 |

### 1.5 Cache Miss Behavior (3 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 23 | PU-MISS-001 | Cache miss populates IMemoryCache from DB | Cache does not contain requested table metadata | Repository returns correct data for table | On first access, data fetched from repo and stored in IMemoryCache; subsequent reads come from cache | Table 1 in DB |
| 24 | PU-MISS-002 | Cache miss populates Redis from DB | Local cache does not have entry, Redis does not either | Both caches empty, DB has data | Distributed cache entry created after local cache miss | Table 1 in DB |
| 25 | PU-MISS-003 | Cache miss with missing DB data returns empty/exception | Cache miss, repository returns nothing | Repository returns null or empty for table | Empty or null returned; no stale data served | Table not in DB |

### 1.6 Cache Refresh / Graph-Based Invalidation (4 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 26 | PU-REFRESH-001 | Unit | After metadata commit, affected cache entries invalidated | SysColumn updated; graph resolves dependency | Only cache entries for affected table and dependent tables invalidated; unrelated tables remain cached | Update SysColumn in table "Users"; invalidate "meta:table:Users" |
| 27 | PU-REFRESH-002 | Unit | Dictionary mutation invalidates node-local cache before distributed | Transaction commit succeeds | IMemoryCache.Remove called for all affected keys; then Redis publish event | Keys: "meta:table:1", "meta:column:1" |
| 28 | PU-REFRESH-003 | Unit | Failed transaction does NOT invalidate cache | Transaction rolls back | No cache entries removed; no Redis event published; previous cache state intact | Exception during commit |
| 29 | PU-REFRESH-004 | Unit | Graph-based invalidation cascades to dependent tables | Table A references Table B (via SysReferenceTable); B is updated | Both "meta:table:B" and "meta:table:A" invalidated; B first, then A | FK relationship B->A |

### 1.7 Type Validation (7 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 30 | PU-TYPE-001 | Type validator accepts value matching SysColumn element type | SysColumn.SysElementId=1 (VARCHAR); value is string | TypeValidator.Validate(Element.VarChar, "hello") | No exception; validation passes | "hello" |
| 31 | PU-TYPE-002 | Type validator rejects value not matching SysColumn element type | SysColumn.SysElementId=1 (INTEGER); value is string | TypeValidator.Validate(Element.Integer, "not-a-number") | `BusinessRuleException` thrown with type mismatch message | "not-a-number" |
| 32 | PU-TYPE-003 | Type validator enforces FieldLength maximum | SysColumn.FieldLength=10; value is 15 chars | TypeValidator.Validate with length constraint | `BusinessRuleException` thrown for exceeding FieldLength | "123456789012345" (15 chars) |
| 33 | PU-TYPE-004 | Type validator allows value equal to FieldLength | SysColumn.FieldLength=10; value is 10 chars | No exception thrown | Validation passes | "1234567890" |
| 34 | PU-TYPE-005 | Type validator accepts null when IsMandatory=false | SysColumn.IsMandatory=false; value is null | No exception thrown | Validation passes | null |
| 35 | PU-TYPE-006 | Type validator rejects null when IsMandatory=true | SysColumn.IsMandatory=true; value is null | `BusinessRuleException` thrown | Message includes column name | null |
| 36 | PU-TYPE-007 | Type validator enforces ValueMin/ValueMax for numeric types | SysColumn.ValueMin="1", ValueMax="100"; value=0 | `BusinessRuleException` thrown | Message includes range violation | 0 (below min of 1) |

### 1.8 Reference Validation (4 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 37 | PU-REF-001 | Reference validator accepts valid LIST value | SysReference of type LIST with values "A","B","C"; input="A" | ReferenceValueValidator.EnsureInSet returns without exception | Validation passes | "A" |
| 38 | PU-REF-002 | Reference validator rejects invalid LIST value | SysReference of type LIST with values "A","B","C"; input="D" | `BusinessRuleException` thrown | Message includes invalid value and allowed set | "D" |
| 39 | PU-REF-003 | Reference validator rejects table reference with missing key | SysReference of type TABLE; referenced record does not exist in target table | `BusinessRuleException` thrown | Message references missing record | PK=999 not in target table |
| 40 | PU-REF-004 | Reference validator accepts table reference with existing key | SysReference of type TABLE; referenced record exists | No exception | Validation passes | PK=1 exists in target table |

### 1.9 ValRule Evaluation (5 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 41 | PU-VALRULE-001 | ValRule engine evaluates SQL rule type successfully | SysValRule RuleType=SQL, Code="VALUE > 0"; value=5 | Evaluator returns true | Validation passes | 5 |
| 42 | PU-VALRULE-002 | ValRule engine evaluates SQL rule type failure | SysValRule Code="VALUE > 0"; value=-1 | Evaluator returns false; `BusinessRuleException` thrown | Validation fails with rule name in message | -1 |
| 43 | PU-VALRULE-003 | ValRule engine evaluates Regex rule type successfully | SysValRule RuleType=Regex, Code="^[A-Z]{3}$"; value="ABC" | Evaluator returns true | Validation passes | "ABC" |
| 44 | PU-VALRULE-004 | ValRule engine evaluates Regex rule type failure | SysValRule Code="^[A-Z]{3}$"; value="abc" | Evaluator returns false; `BusinessRuleException` thrown | Validation fails | "abc" |
| 45 | PU-VALRULE-005 | ValRule engine rejects unsafe SQL patterns | SysValRule Code="DROP TABLE Users;"; value="x" | `SecurityException` or validation rejected by expression evaluator | Unsafe SQL patterns are blocked | "DROP TABLE Users;" |

### 1.10 Context Variables (3 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 46 | PU-CTX-001 | Context resolver returns $UserId from ambient context | HttpContext/claims principal set with userId claim | ContextVariableResolver.Resolve("$UserId") returns string ID | Returns "42" | userId=42 in principal |
| 47 | PU-CTX-002 | Context resolver returns $TenantId from ambient context | TenantId set in scoped service | ContextVariableResolver.Resolve("$TenantId") returns string ID | Returns "tenant-A" | tenantId="tenant-A" |
| 48 | PU-CTX-003 | Context resolver returns $Timestamp in ISO 8601 | Current time set in test | ContextVariableResolver.Resolve("$Timestamp") returns valid ISO 8601 string | Format matches "yyyy-MM-ddTHH:mm:ss.fffZ" | N/A (runtime) |

### 1.11 PO Validation Pipeline (4 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 49 | PU-PO-001 | PO validation pipeline runs mandatory then type then reference then ValRule | Full pipeline wired with all validators | Validation order enforced: mandatory -> type -> reference -> ValRule | First failing validator stops pipeline; error message from that step returned | Column mandatory + type mismatch + ValRule fail: type error reported |
| 50 | PU-PO-002 | PO validation passes all stages | All validators configured; value is valid | No exceptions; ValidationResult.Success = true | Pipeline completes; result contains no errors | Valid data |
| 51 | PU-PO-003 | PO validation returns all errors (not first-only) | Multiple validators set to fail | All validator results collected; no short-circuiting | Multiple error messages in result; caller can display all | Type fails + ValRule fails |
| 52 | PU-PO-004 | PO validation with no ValRule attached skips ValRule stage | SysColumn with SysValRuleId=null | ValRule evaluator not invoked; no null reference exception | Pipeline completes successfully if other stages pass | SysValRuleId = null |

### 1.12 PO Lifecycle Hooks (4 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 53 | PU-LIFE-001 | PO beforeCreate hook is invoked before persist | PO.Create() called with M_<Table> implementing BeforeCreate | Hook method executes; sets CreatedAt to current timestamp | CreatedAt field set before INSERT | M_Users.BeforeCreate sets CreatedAt |
| 54 | PU-LIFE-002 | PO afterCreate hook is invoked after persist | PO.Save() completes INSERT | Hook method executes with persisted entity (ID assigned) | AfterCreate receives entity with SysTableId populated | Any valid entity |
| 55 | PU-LIFE-003 | PO beforeUpdate hook is invoked with old and new values | PO.Load(id) then set field then Save() | BeforeUpdate receives snapshot of old values and current values | Old values available for audit comparison | Old Name="A", New Name="B" |
| 56 | PU-LIFE-004 | PO delete hook M_<Table>.BeforeDelete can veto | M_<Table> throws `BusinessRuleException` in BeforeDelete | PO.Delete() propagates exception; no DELETE executed | Record not deleted; exception bubbled to caller | Business rule: cannot delete in-use record |

### 1.13 PO Factory (3 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 57 | PU-FACTORY-001 | Factory resolves X_<Table> class from metadata | SysTable.ClassName="X_Users" in DB | Factory creates instance of X_Users type | Type is assignable from expected base PO class | SysTable.TableName="Users" |
| 58 | PU-FACTORY-002 | Factory resolves M_<Table> class from metadata | SysTable.ClassName="M_Users" in DB | Factory creates instance of M_Users type | M_Users instance returned; business methods available | SysTable.TableName="Users" |
| 59 | PU-FACTORY-003 | Factory handles missing assembly gracefully | Assembly containing M_<Table> not loaded | `FileNotFoundException` or `TypeLoadException` caught; descriptive error returned | Application does not crash; error logged; fallback to generic PO | M_Users.dll not deployed |

### 1.14 Tenant/Org Predicates (3 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 60 | PU-PRED-001 | Query builder injects tenant predicate into WHERE clause | TenantId=1 in context; query requests records | WHERE clause contains "TenantId = @p0" with parameterized value | Parameter "@p0" = "1"; no raw SQL injection | TenantId = 1 |
| 61 | PU-PRED-002 | Query builder injects org predicate into WHERE clause | OrgId=5 in context | WHERE clause contains "OrgId = @p1" | Parameter "@p1" = "5"; predicates combined with AND | OrgId = 5 |
| 62 | PU-PRED-003 | Predicates cannot be bypassed by client-supplied filter | Client requests records without TenantId in their filter | Predicates always appended; results scoped to current tenant/org | Cross-tenant records never returned regardless of client input | Client filter: no tenant |

### 1.15 Parameterization (2 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 63 | PU-PARAM-001 | All generated SQL uses parameterized values | QueryBuilder constructs SELECT for table | SQL string contains "@param" placeholders, not inline values | No string concatenation of user input in final SQL | User input: "Robert'); DROP TABLE --" |
| 64 | PU-PARAM-002 | Dynamic SQL identifiers validated against metadata | QueryBuilder receives table name from API | Identifier looked up in metadata; if not found, exception thrown | Unsafe table name "Users; DROP TABLE" rejected; valid table name allowed | "Users" (valid), "Users; DROP TABLE" (invalid) |

---

## 2. Integration Tests (24)

### Infrastructure Requirements

All integration tests require:
- **PostgreSQL service container** via Testcontainers.PostgreSql (same approach as Phase 1 integration tests in `DictionaryMigrationTests.cs`)
- **Redis container** via Testcontainers.StackExchangeRedis
- Connection strings passed via `NCLC_TEST_CONNECTION_STRING` environment variable in CI; TestContainers used locally
- Both containers started in `InitializeAsync()` (xunit `IAsyncLifetime`) and disposed in `DisposeAsync()`

### 2.1 Metadata Graph Integration (2 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 65 | PI-GRAPH-001 | Integration | Full graph construction from live DB matches expected topology | PostgreSQL with seed data; MetadataGraph service wired | Graph node count matches active SysTable count; column relationships correct | All seed data tables |
| 66 | PI-GRAPH-002 | Integration | Dependency resolution order is valid for live metadata | PostgreSQL with metadata containing references | Topological sort produces order where referenced tables come before referencing tables | Tables with FK relationships |

### 2.2 Metadata Loading from PostgreSQL (3 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 67 | PI-LOAD-001 | Integration | LoadAll returns consistent data with direct SQL query | PostgreSQL with seed data | Metadata snapshot row count matches `SELECT COUNT(*)` for each Sys* table | All seed data |
| 68 | PI-LOAD-002 | Integration | LoadByTable returns correct subset | PostgreSQL with multiple tables | Requested table data matches; other tables excluded | Request table "SysReference" |
| 69 | PI-LOAD-003 | Integration | Load reflects recent inserts (no stale cache on first access) | Database with new row inserted via raw SQL before load | New row appears in loaded metadata | Insert SysTable with TableName="TestNewTable" |

### 2.3 IMemoryCache + DB Round-Trip (3 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 70 | PI-CACHE-001 | Integration | Cache hit path: metadata served from IMemoryCache | Data loaded once into cache; second access requested | Second access served from cache (not DB); verified via SQL logging | SysTable loaded twice |
| 71 | PI-CACHE-002 | Integration | Cache miss path: metadata loaded from DB on first access | Fresh cache; first access requested | DB query executed; data stored in cache | Any table |
| 72 | PI-CACHE-003 | Integration | Cache invalidated after metadata update | Update SysColumn via repository; clear cache for that table | Subsequent load returns updated data; old data no longer served | Update SysColumn.FieldLength from 50 to 100 |

### 2.4 Redis Distributed Cache (3 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 73 | PI-REDIS-001 | Integration | Distributed cache write and read across two cache instances | Two IMemoryCache instances, shared Redis | Instance A writes; Instance B reads from distributed cache | Key "meta:table:1" |
| 74 | PI-REDIS-002 | Integration | Distributed cache invalidation propagates | Instance A invalidates; Instance B subscribed | Instance B receives invalidation event within timeout; stale entry removed | Event published on "metadata:invalidation" |
| 75 | PI-REDIS-003 | Integration | Distributed cache degrades gracefully when Redis unavailable | Redis container stopped | Application continues serving from local IMemoryCache; no exceptions bubble to API | Any cache operation |

### 2.5 Cache Miss and Refresh (3 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 76 | PI-MISS-001 | Integration | Cache miss triggers DB load and populates local + distributed cache | Both caches empty, DB has data | First access hits DB; both caches populated | Table not in any cache |
| 77 | PI-MISS-002 | Integration | Cache refresh after metadata transaction | Insert SysColumn via API; invalidate affected table cache | New column appears on next load; old cache entries cleared | Insert new SysColumn |
| 78 | PI-MISS-003 | Integration | Cache never serves stale data after successful update | Update metadata; invalidate; read | Read returns new data; intermediate stale read confirmed impossible (assert on timing window) | Update SysTable.TableName |

### 2.6 Type Validation (3 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 79 | PI-TYPE-001 | Integration | PO.Save() validates type against SysColumn metadata | SysColumn registered with specific element type; value passed via API | Valid type accepted; invalid type rejected with 400 response | VARCHAR column, integer value sent |
| 80 | PI-TYPE-002 | Integration | FieldLength enforced via POST to generic data API | SysColumn.FieldLength=10; POST body exceeds length | API returns 400 with validation error | 15-char string to 10-char column |
| 81 | PI-TYPE-003 | Integration | Type round-trip through Dapper preserves enum values | SysColumn with SysElementId referencing element; load via repository | Dapper type handler correctly round-trips ValidationType enum | Element type = Integer |

### 2.7 Reference Validation (3 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 82 | PI-REF-001 | Integration | LIST reference enforced via generic data API | SysReference type LIST with values; SysColumn references it | POST with valid list value succeeds; invalid value returns 400 | List: "Active","Inactive" |
| 83 | PI-REF-002 | Integration | TABLE reference validated against target table | SysReferenceTable defined; SysColumn references it | POST with valid FK value succeeds; invalid FK returns 400 | Target PK=1 exists |
| 84 | PI-REF-003 | Integration | Reference cascade: deleting referenced record rejected if in use | Target record referenced by source record | DELETE on target returns 409 Conflict or 400; reference maintained | FK points to PK=1 in target |

### 2.8 ValRule Evaluation (3 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 85 | PI-VALRULE-001 | Integration | SQL ValRule enforced via POST | SysValRule RuleType=SQL, Code="VALUE > 0" | Valid value accepted; invalid returns 400 | Value=5 (pass), Value=-1 (fail) |
| 86 | PI-VALRULE-002 | Integration | Regex ValRule enforced via POST | SysValRule RuleType=Regex, Code="^[0-9]{5}$" | Valid postal code accepted; invalid rejected | "12345" (pass), "abcde" (fail) |
| 87 | PI-VALRULE-003 | Integration | ValRule code length enforced against SysColumn.Code VARCHAR(2000) | Insert ValRule with Code=2001 chars | PostgreSQL uniqueness/length constraint triggers; 400 response returned | 2001-char code string |

### 2.9 Context Variables (2 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 88 | PI-CTX-001 | Integration | $UserId injected into CreatedBy on PO create | User authenticated; ClaimsPrincipal set | Persisted record has CreatedBy = authenticated user ID | User "admin" |
| 89 | PI-CTX-002 | Integration | $TenantId applied to every query via predicate | Tenant context set; query executed | SQL includes "WHERE TenantId = @p0"; results scoped to tenant | TenantId = "tenant-A" |

### 2.10 PO Lifecycle (2 tests)

| # | Test ID | Category | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|---|
| 90 | PI-LIFE-001 | Integration | PO lifecycle hooks persist audit data | BeforeCreate/AroundSave hooks registered | Audit table contains record with old/new values | Create with Name="Test" |
| 91 | PI-LIFE-002 | Integration | M_<Table> business rule blocks invalid operation via API | M_<Table> enforces business rule | API returns 422 Unprocessable Entity with business rule message | Business rule violation |

---

## 3. Regression Tests (67)

All 67 Phase 1 tests must continue to pass. They are partitioned into three test assemblies:

### 3.1 Unit Tests — 24 tests (no change, must still pass)

From `Platform.Tests.Core` (xUnit + FluentAssertions):

| # | Assembly | Test File | Count |
|---|---|---|---|
| 92-94 | Core | `Dictionary/Models/SysElementTests.cs` | 3 |
| 95-97 | Core | `Dictionary/Models/SysReferenceTests.cs` | 3 |
| 98-100 | Core | `Dictionary/Models/SysReferenceListTests.cs` | 3 |
| 101-103 | Core | `Dictionary/Models/SysTableTests.cs` | 3 |
| 104-107 | Core | `Dictionary/Models/SysColumnTests.cs` | 4 |
| 108-110 | Core | `Dictionary/Models/SysTranslationTests.cs` | 3 |
| 111-113 | Core | `Dictionary/Models/SysValRuleTests.cs` | 3 |
| 114-115 | Core | `PlatformTests.cs` | 2 |
| 116-124 | Core | (additional model validation tests in Phase 1) | 9 |

**Subtotal: 24 unit tests**

### 3.2 Schema Contract Tests — 33 tests (no change, must still pass)

From `Platform.Tests.SchemaContract` (xUnit, live PostgreSQL):

| # | Assembly | Test File | Count | Key Assertions |
|---|---|---|---|---|
| 125-138 | SchemaContract | `SchemaContractTests.cs` | 14 | SysElement columns, types, uniqueness, defaults |
| 139-144 | SchemaContract | `SchemaContractTests.cs` | 6 | SysElement_Trl composite PK, Language length |
| 145-160 | SchemaContract | `SchemaContractTests.cs` | 8 | SysReference columns, uniqueness, ValidationType |
| 161-172 | SchemaContract | `SchemaContractTests.cs` | 6 | SysReferenceList constraints, Value/Name lengths |
| 173-187 | SchemaContract | `SchemaContractTests.cs` | 5 | SysReferenceTable columns, FK, KeyColumn length |
| 188-203 | SchemaContract | `SchemaContractTests.cs` | 8 | SysValRule Name unique/length, RuleType default, Code length |
| 204-219 | SchemaContract | `SchemaContractTests.cs` | 8 | SysTable TableName unique, AccessLevel default, EntityType default |
| 220-243 | SchemaContract | `SchemaContractTests.cs` | 12 | SysColumn columns, FKs, constraints, lengths |
| 244-259 | SchemaContract | `SchemaContractTests.cs` | 6 | SysColumn SysReference_ID FK, EntityType/MaxLength |
| 260-272 | SchemaContract | `SchemaContractTests.cs` | 8 | FK constraints: SysColumn.SysTable_ID, SysColumn.SysReference_ID, SysReferenceTable.SysReference_ID |
| 273-288 | SchemaContract | `SchemaContractTests.cs` | 4 | All 8 tables exist |
| 289-298 | SchemaContract | `SchemaContractTests.cs` | 10 | SysColumn column count (22), SysReference_ID NOT NULL, etc. |

**Subtotal: 33 schema contract tests**

### 3.3 Integration Tests — 10 tests (no change, must still pass)

From `Platform.Tests.Integration` (xUnit + Testcontainers.PostgreSql):

| # | Assembly | Test File | Count |
|---|---|---|---|
| 299-308 | Integration | `DictionaryMigrationTests.cs` | 10 |

Key assertions verified in regression:
- All 8 dictionary tables exist
- SysColumn has all required columns
- SysTable has all required columns
- Seed data counts: SysReference >= 11, SysValRule >= 2, SysTable >= 7, SysElement >= 27, SysReferenceTable >= 1
- Foreign keys have no orphans
- UNIQUE constraints enforced (23505 PostgreSQL error code)

**Subtotal: 10 integration tests**

**Total Regression: 24 + 33 + 10 = 67 tests**

---

## Test Data Setup

### Shared Test Data

The following seed data (from Phase 1) provides baseline coverage:

| Entity | Minimum Rows | Used For |
|---|---|---|
| SysElement | 27 | Type validation, graph construction |
| SysReference | 11 | Reference validation (LIST, TABLE, SEARCH) |
| SysReferenceList | Varies (per reference) | LIST reference values |
| SysReferenceTable | 1 | TABLE reference validation |
| SysValRule | 2 | SQL and Regex rule evaluation |
| SysTable | 7 | Graph construction, loading, caching |
| SysColumn | 22+ | Type validation, length enforcement |

### Phase 2-Specific Test Data

Additional data required for Phase 2 tests:

| Entity | Purpose | Example |
|---|---|---|
| SysTable (additional) | Graph dependency chains | 3+ tables with FK relationships |
| SysColumn (additional) | ValRule-attached columns | 3+ columns with SysValRuleId set |
| SysValRule (additional) | SQL/Regex/Lambda rule types | 2 SQL rules, 2 regex rules, 1 lambda |
| SysReference (additional) | TABLE reference type | Reference with ValidationType=Table |
| SysReferenceTable (additional) | Table-based reference definition | ReferenceTable with KeyColumn/DisplayColumn |

---

## Container Requirements

| Service | Image | Purpose | Used By |
|---|---|---|---|
| PostgreSQL 15+ | `postgres:15-alpine` | Metadata store | All integration tests (24 PI + 10 regression) |
| Redis | `redis:7-alpine` | Distributed cache + pub/sub | Redis tests (PI-REDIS-001 through 003, PU-REDIS-001 through 005) |

Containers started in test class `InitializeAsync()` via `IAsyncLifetime`. Pattern matches existing `DictionaryMigrationTests.cs`.

---

## Mapping to HLD/LLD Implementation Items

| HLD/LLD #35 Item | Test Coverage |
|---|---|
| 11. Upgrade MetaColumn | PU-TYPE-001 through 007, PI-TYPE-001 through 003 |
| 12. Upgrade metadata cache graph | PU-GRAPH-001 through 005, PI-GRAPH-001 through 002 |
| 13. Add base type validation | PU-TYPE-001 through 007, PI-TYPE-001 through 003 |
| 14. Add reference validation | PU-REF-001 through 004, PI-REF-001 through 003 |
| 15. Add ValRule evaluation | PU-VALRULE-001 through 005, PI-VALRULE-001 through 003 |
| 16. Add runtime context-variable resolution | PU-CTX-001 through 003, PI-CTX-001 through 002 |
| 17. Strengthen PO validation | PU-PO-001 through 004, PI-TYPE-001, PI-REF-001, PI-VALRULE-001 |
| 18. Strengthen PO lifecycle hooks | PU-LIFE-001 through 004, PI-LIFE-001 through 002 |
| 19. Strengthen PO factory/class resolution | PU-FACTORY-001 through 003 |
| (Implicit from Sec 25) Metadata cache | PU-CACHE-001 through 008, PU-REDIS-001 through 005, PU-MISS-001 through 003, PU-REFRESH-001 through 004 |
| (Implicit from Sec 12) Tenant/org predicates | PU-PRED-001 through 003, PI-CTX-002 |
| (Implicit from Sec 12) Parameterization | PU-PARAM-001 through 002 |

---

## Verification Checklist

Before declaring Phase 2 testing complete:

- [ ] All 45 unit tests pass (`dotnet test Platform.Tests.Core`)
- [ ] All 24 integration tests pass (`dotnet test Platform.Tests.Integration`) with containers available
- [ ] All 67 Phase 1 regression tests still pass (all three test assemblies)
- [ ] `scripts/phase-gate.ps1` returns exit code 0
- [ ] No test uses weakened assertions to pass
- [ ] No test depends on UI as a security boundary
- [ ] No test injects raw SQL from user input
- [ ] Cross-tenant access is proven rejected in at least one test (PI-PRED-002 or equivalent)
- [ ] Stale metadata proven unserved after cache invalidation (PU-REFRESH-003, PI-MISS-003)

---

## File Paths

- Test matrix (this file): `C:\Project\NCLC\NoCodeLow\docs\testing\PHASE-2-TEST-MATRIX.md`
- Phase 1 unit tests: `C:\Project\NCLC\NoCodeLow\tests\Platform.Tests.Core\Dictionary\Models\*.cs`
- Phase 1 integration tests: `C:\Project\NCLC\NoCodeLow\tests\Platform.Tests.Integration\DictionaryMigrationTests.cs`
- Phase 1 schema contract tests: `C:\Project\NCLC\NoCodeLow\tests\Platform.Tests.SchemaContract\SchemaContractTests.cs`
- Phase gate script: `C:\Project\NCLC\NoCodeLow\scripts\phase-gate.ps1`
- Phase state: `C:\Project\NCLC\NoCodeLow\docs\agent-state\ACTIVE.md`
- Architecture specification: `C:\Project\NCLC\NoCodeLow\docs\architecture\FINAL-MASTER-HLD-LLD-v2.md`
