# Phase 2 Security Findings Register

## Summary

| Severity | Count | Status |
|----------|-------|--------|
| CRITICAL | 2 | Requires Phase 2 implementation |
| HIGH | 2 | Requires Phase 2 implementation |
| MEDIUM | 3 | Requires Phase 2 implementation |
| LOW | 1 | Requires Phase 2 implementation |
| **Total** | **8** | |

## Source

- Primary: `docs/security/PHASE-2-SECURITY-REVIEW.md` (427 lines)
- Revalidation: `docs/agent-state/PHASE-2-PREFLIGHT-REVALIDATION.md` (252 lines)

Both documents agree: **2C, 2H, 3M, 1L = 8 findings**

## Finding Register

### Finding 1: SQL Injection via ValRule.Code (CRITICAL)

- **ID:** SEC-P2-001
- **Severity:** CRITICAL
- **Description:** `ValRuleEngine.EvaluateSql()` constructs SQL from `SysValRule.Code` stored in DB. The Code column could contain malicious SQL if the seed/seed data is tampered with or if an attacker gains DB write access.
- **Affected component:** `src/Platform.Core/Runtime/ValRuleEngine.cs`
- **HLD requirement:** Section 8.3 — SQL rules must be SELECT-only, parameterized, function-whitelisted
- **Required mitigation:**
  - SELECT-only check (regex on leading statement)
  - Disallowed SQL keywords check (INSERT, UPDATE, DELETE, DROP, EXEC, etc.)
  - Function whitelist (COUNT, SUM, AVG, etc.)
  - Always parameterize @Value — never concatenate
  - Timeout (100ms)
- **Implementation status:** DONE (in ValRuleEngine.cs)
- **Automated test:** ValRuleEngineTests — SQL injection attempt, disallowed keywords, function whitelist
- **CI coverage:** Unit test in Platform.Tests.Core
- **ADR:** ADR-ValRule-Security (preparation)
- **Phase:** 2
- **Status:** IMPLEMENTED, needs test verification

**BUG NOTE:** CTE bypass exists — `WITH ( AS ...` could slip past the SELECT check because the regex checks if trimmed SQL starts with "SELECT", and `( ` at the start is also allowed. This is a P0 fix needed.

### Finding 2: Arbitrary Class Instantiation via POFactory (HIGH)

- **ID:** SEC-P2-002
- **Severity:** HIGH
- **Description:** `POFactory.CreatePO()` accepts a class name string and instantiates via reflection. Without proper assembly whitelist and type validation, an attacker could instantiate arbitrary classes.
- **Affected component:** `src/Platform.Metadata/Factory/POFactory.cs`
- **HLD requirement:** Section 8.4 — PO Factory must use assembly whitelist and type name validation
- **Required mitigation:**
  - Assembly whitelist: only `Platform.Metadata` and known runtime assemblies
  - Type name regex: `^(M_|X_)\w+$`
  - Must implement `IPersistentObject`
  - Must not allow `System.*` or `Microsoft.*` types
- **Implementation status:** DONE (in POFactory.cs)
- **Automated test:** POFactoryTests — whitelist, regex, IPersistentObject check
- **CI coverage:** Unit test in Platform.Tests.Core
- **ADR:** ADR-PO-Factory-Security (preparation)
- **Phase:** 2
- **Status:** IMPLEMENTED, needs test verification

### Finding 4: Context Variable Injection (CRITICAL)

- **ID:** SEC-P2-004
- **Severity:** CRITICAL
- **Description:** `ContextVariableResolver` returns `null` for all variables. If later implemented without proper validation, untrusted context variables could be injected into validation rules or queries.
- **Affected component:** `src/Platform.Core/Runtime/ContextVariableResolver.cs`
- **HLD requirement:** Section 8.2 — Context variables must come from trusted sources (HTTP headers, not user input)
- **Required mitigation:**
  - Only read from trusted HTTP headers (X-Tenant, X-User, X-Org)
  - Validate type and length of all context values
  - Never read from query string, body, or cookies
  - Fixed set of variable names — no dynamic names
- **Implementation status:** DONE (stub that returns null)
- **Automated test:** ContextVariableResolverTests — trusted headers, untrusted input rejection
- **CI coverage:** Unit test in Platform.Tests.Core
- **Phase:** 2
- **Status:** STUB IMPLEMENTED, full implementation + tests needed

### Finding 5: Lambda ValRule Arbitrary Code Execution (HIGH)

- **ID:** SEC-P2-005
- **Severity:** HIGH
- **Description:** Lambda ValRule type allows arbitrary C# code execution. In Phase 2, Lambda rules are not supported and should return "not supported" error.
- **Affected component:** `src/Platform.Core/Runtime/ValRuleEngine.cs`
- **HLD requirement:** Section 8.3 — Lambda rules require pre-registered delegates in a safe sandbox
- **Required mitigation:**
  - Phase 2: Reject all Lambda rules with "not supported" error
  - Phase 3+: Pre-registered delegates only, from trusted source
  - No eval/Expression.Compile from user input
- **Implementation status:** DONE (returns "not supported" error)
- **Automated test:** ValRuleEngineTests — Lambda rejection
- **CI coverage:** Unit test in Platform.Tests.Core
- **Phase:** 2
- **Status:** IMPLEMENTED

### Finding 3: Cache Poisoning via DictionaryChangedEvent (MEDIUM)

- **ID:** SEC-P2-003
- **Severity:** MEDIUM
- **Description:** `CacheInvalidationService` publishes events via Redis pub/sub. If an attacker can inject malformed events, they could trigger cache invalidation floods (denial of service).
- **Affected component:** `src/Platform.Core/Cache/CacheInvalidationService.cs`
- **HLD requirement:** Section 8.5 — Cache invalidation must be post-commit and well-formed
- **Required mitigation:**
  - Validate event format before publishing
  - Rate limit invalidation events per source
  - Ignore malformed events gracefully (already implemented)
  - Never trust events from external sources on the Redis channel
- **Implementation status:** DONE (malformed events are ignored, but no rate limiting)
- **Automated test:** CacheInvalidationServiceTests — malformed event handling
- **CI coverage:** Unit test in Platform.Tests.Core
- **Phase:** 2
- **Status:** PARTIALLY IMPLEMENTED (validation done, rate limiting deferred)

### Finding 6: ValRule SQL Denial of Service (MEDIUM)

- **ID:** SEC-P2-006
- **Severity:** MEDIUM
- **Description:** A complex SQL ValRule could cause long-running queries even with SELECT-only check. Timeout exists (100ms) but Npgsql.CommandTimeout is in seconds and set too low (0 seconds = infinite).
- **Affected component:** `src/Platform.Core/Runtime/ValRuleEngine.cs`
- **HLD requirement:** Section 8.3 — All dynamic queries must have timeouts
- **Required mitigation:**
  - Npgsql.CommandTimeout must be >= 1 second (100ms / 1000 = 0, which means infinite!)
  - Fix: use `Math.Max(1, SqlTimeoutMs / 1000)` or set to a minimum of 1
- **Implementation status:** BUG — CommandTimeout is set to 0 (infinite)
- **Automated test:** ValRuleEngineTests — slow SQL returns timeout, not hang
- **CI coverage:** Unit test in Platform.Tests.Core
- **Phase:** 2
- **Status:** BUG DETECTED — P0 fix needed

**BUG NOTE:** `cmd.CommandTimeout = SqlTimeoutMs / 1000` → `100 / 1000 = 0` in integer division. Npgsql treats 0 as "use default" which is 30 seconds. The fix is to use `Math.Max(1, SqlTimeoutMs / 1000)` or just hardcode a reasonable timeout.

### Finding 7: Metadata Enumeration (LOW)

- **ID:** SEC-P2-007
- **Severity:** LOW
- **Description:** `MetadataGraph` exposes all metadata including column types, references, and ValRule codes. An unauthenticated user who can reach the API could enumerate the entire schema.
- **Affected component:** `src/Platform.Core/Runtime/MetadataGraph.cs`
- **HLD requirement:** Section 8.1 — Metadata is internal use only; API endpoints require auth
- **Required mitigation:**
  - All API endpoints in Platform.API must require authentication (AddAuthorization)
  - MetadataGraph is internal — never exposed directly as an API endpoint
  - No public GET /metadata endpoint
- **Implementation status:** Deferred — no auth in Platform.API yet
- **Automated test:** N/A — will be covered by auth implementation
- **CI coverage:** Will be covered when Phase 3 adds auth
- **ADR:** ADR-Auth-Strategy (to be created)
- **Phase:** 2 (observation only, auth in Phase 3)
- **Status:** DEFERRED to Phase 3 (auth implementation)

### Finding 8: Unrestricted Cache Memory Usage (MEDIUM)

- **ID:** SEC-P2-008
- **Severity:** MEDIUM
- **Description:** `MetadataCacheService` uses `IMemoryCache` without absolute/Sliding expiration limits. A very large metadata set could cause unbounded memory growth.
- **Affected component:** `src/Platform.Core/Cache/MetadataCacheService.cs`
- **HLD requirement:** Section 8.5 — Cache entries must have reasonable limits
- **Required mitigation:**
  - Set SlidingExpiration on all cache entries (e.g., 10 minutes)
  - Set AbsoluteExpirationOnLastAccess for max lifetime (e.g., 1 hour)
  - Consider memory limits via MemoryCacheOptions.SizeLimit
- **Implementation status:** Deferred — no expiration set on cache entries
- **Automated test:** MetadataCacheServiceTests — entries expire correctly
- **CI coverage:** Unit test in Platform.Tests.Core
- **Phase:** 2
- **Status:** NEEDS IMPLEMENTATION

## P0 Bugs (from Phase 2 Verification)

| ID | Description | Finding | Status |
|----|-------------|---------|--------|
| P0-1 | N+1 queries in MetadataGraph.LoadColumns | Performance | Needs fix |
| P0-2 | POValidator returns first error only | Validation completeness | Needs fix |
| P0-3 | ValRuleEngine CTE bypass in IsSelectStatement | SEC-P2-001 | Needs fix |
| P0-4 | ValRuleEngine hardcoded fallback conn string | Reliability | Needs fix |
| P0-5 | ReferenceValueValidator TABLE/SEARCH unconditional pass | SEC-P2-002 | Needs fix |
| P0-6 | CommandTimeout = 0 (infinite) | SEC-P2-006 | Needs fix |

## P1 Bugs

| ID | Description | Finding | Status |
|----|-------------|---------|--------|
| P1-1 | CacheInvalidationService.InvalidateByEvent is no-op | SEC-P2-003 | Needs fix |
| P1-2 | POLifecycleManager.GetTableInfo always returns null | Reliability | Needs fix |
| P1-3 | POLifecycleManager no deterministic hook ordering | Reliability | Needs fix |
| P1-4 | ContextVariableResolver always returns null | SEC-P2-004 | Stub only |
| P1-5 | POLifecycleManager no validation enforcement | Validation | Needs fix |

## Action Items

1. **Immediate (P0):** Fix all 6 P0 bugs in ValRuleEngine, POValidator, MetadataGraph, ReferenceValueValidator
2. **Next (P1):** Fix cache invalidation, lifecycle manager, context resolver
3. **Next:** Write 45 unit tests + 24 integration tests
4. **Deferred:** Auth (Finding 7 — Phase 3)
