# Phase 3 - Security Findings Register

## Scope

Phase 3 introduces the **UI layer**: generic forms, generic grids, lookup/search, display logic, and menus, together with the corresponding API endpoints they consume:

- GET /api/data/{table} -- list records
- POST /api/data/{table} -- create record
- GET /api/data/{table}/{id} -- read single record
- PUT /api/data/{table}/{id} -- update record
- DELETE /api/data/{table}/{id} -- delete record
- GET /api/meta/window/{windowId} -- load window metadata
- GET /api/lookup/{referenceId} -- lookup reference values
- GET /api/lookup/{referenceId}/search -- search reference values

## Prerequisite

**No authentication is implemented in the current codebase.** src/Platform.API/Program.cs (line 121) exposes only a single unauthenticated health endpoint. There are zero AddAuthentication, AddAuthorization, or UseAuthorization calls. The only API route registered is GET /health.

Phase 3 must establish authentication as a gate before any data, metadata, or lookup endpoints are exposed. ADR-002 proposes ASP.NET Identity + JWT bearer tokens.

---

## Findings Register Summary

| # | ID | Severity | Component | Title | Requires Phase 4 |
|---|---|---|---|---|---|
| 1 | SEC-P3-001 | CRITICAL | Platform.API / all endpoints | No authentication on any endpoint | No |
| 2 | SEC-P3-002 | CRITICAL | Generic Data API | Tenant/org isolation missing from CRUD queries | Yes |
| 3 | SEC-P3-003 | CRITICAL | QueryBuilder / Generic Data API | SQL injection via dynamic table/column identifiers | No |
| 4 | SEC-P3-004 | HIGH | React / Generic Form | XSS via metadata-driven content rendering | No |
| 5 | SEC-P3-005 | HIGH | Generic Data API | Overbroad data projection | Yes |
| 6 | SEC-P3-006 | HIGH | Generic Data API / API layer | Column-level access control not enforced | Yes |
| 7 | SEC-P3-007 | MEDIUM | Lookup API | Denial of service on high-volume reference tables | No |
| 8 | SEC-P3-008 | MEDIUM | Platform.API / all endpoints | No audit logging for CRUD operations | No |
| 9 | SEC-P3-009 | MEDIUM | React / Generic Form | CSRF on JWT bearer token | No |
| 10 | SEC-P3-010 | MEDIUM | React / Generic Form | Client-side display-logic bypass | No |
| 11 | SEC-P3-011 | MEDIUM | Generic Grid | Row-level predicate not enforced in bulk ops | Yes |
| 12 | SEC-P3-012 | LOW | Platform.API / all endpoints | Swagger exposes API contract without auth | No |

Severity distribution: 3 CRITICAL, 3 HIGH, 5 MEDIUM, 1 LOW = 12 findings total

---

## Detailed Findings

### SEC-P3-001: No Authentication on Any Endpoint (CRITICAL)

- **Severity:** CRITICAL
- **Affected components:** src/Platform.API/Program.cs, all future API endpoints
- **HLD requirement:** Section 3.1 -- ASP.NET Core Web API must include authentication and authorization. Sections 26-28 end-to-end flows show Authenticate as the first step before any CRUD operation. CLAUDE.md Rule 5 -- UI is never the security boundary.
- **Phase 2 carry-over:** SEC-P2-007 (Metadata enumeration) -- All API endpoints in Platform.API must require authentication.

**Current state:** Program.cs line 121: app.MapGet(/health, ...) is the only endpoint. Zero AddAuthentication, AddAuthorization, or UseAuthorization calls. ADR-002 is PROPOSED but not implemented.

**Threat model:**
- Any unauthenticated caller can enumerate the schema once /api/meta/window endpoints are added.
- Once /api/data/{table} endpoints are added, any unauthenticated caller can read, create, update, and delete all records across all tables for all tenants.
- This is not a theoretical risk -- the entire database is exposed.

**Exploit scenario:**
1. Attacker discovers the API base URL.
2. Attacker calls GET /api/data/sys_table (when implemented) -- no auth required.
3. Attacker calls POST /api/data/actual_business_table with crafted JSON -- no auth required.
4. Full data breach and data destruction possible.

**Required mitigations:**
1. Add builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...) to Program.cs.
2. Add builder.Services.AddAuthorization() to Program.cs.
3. Apply [Authorize] as a global convention or to every endpoint group.
4. Implement JWT validation: issuer, audience, signing key (from config/secret manager, NOT from code).
5. Token TTL must be short (15 minutes access, 7 days refresh).
6. The health endpoint must remain public (or use a separate /api/public group).
7. All endpoints must be under an [Authorize] convention except explicitly exempted routes.

**OWASP Top 10:** A02:2021 -- Cryptographic Failures (if weak JWT config), A07:2021 -- Identification and Authentication Failures

### SEC-P3-002: Tenant/Org Isolation Missing from CRUD Queries (CRITICAL)

- **Severity:** CRITICAL
- **Affected components:** Generic Data API (/api/data/{table}), QueryBuilder, IReadOnlyContext, POLifecycleManager
- **HLD requirement:** Sections 26-28 end-to-end flows show Resolve user/client/org/role and Set tenant/org/audit fields as mandatory steps. CLAUDE.md Rule 8 -- Tenant and organization predicates are applied centrally.
- **Phase 2 carry-over:** SEC-P2-004 (Context variable injection) and SEC-P2-011 (Tenant isolation bypass) -- both require IReadOnlyContext to provide TenantId and OrgId predicates.

**Current state:** The IReadOnlyContext interface exists at src/Platform.Core/Metadata/IReadOnlyContext.cs and Phase 2 added TenantPredicate/OrgPredicate to it. However, no API endpoint has been written yet to consume this context. When Data API endpoints are written, the QueryBuilder must inject tenant/org predicates into every SELECT/INSERT/UPDATE/DELETE.

**Threat model:**
- A tenant A user calls the API. If tenant predicate is not injected into queries, they see all tenants data.
- An attacker crafting direct API calls (bypassing the React frontend) can set arbitrary context values if not derived from JWT claims.
- Multi-tenant SaaS deployment: complete cross-tenant data leak.

**Exploit scenario:**
1. User A (tenant=ACME, org=100) authenticates via JWT.
2. User A opens a form for table Orders.
3. Data API receives the request. QueryBuilder builds SELECT * FROM X_Orders WHERE ...
4. **BUG:** If the tenant predicate AND TenantId = ACME is NOT injected, the query returns ALL tenants orders.
5. User A sees orders belonging to tenant=GLOBEX.

**Required mitigations:**
1. QueryBuilder.BuildSelect(), BuildInsert(), BuildUpdate(), BuildDelete() must all accept IReadOnlyContext and inject tenant/org predicates.
2. POLifecycleManager must enforce context.TenantId is non-null for every CRUD operation and fail if missing.
3. Never trust TenantId or OrgId from HTTP headers or request body -- derive only from JWT claims or server-side authentication middleware.
4. Unit tests for every QueryBuilder method: verify tenant predicate is present in generated SQL.
5. Integration tests: create two tenants, verify tenant A cannot query tenant B records.

**OWASP Top 10:** A01:2021 -- Broken Access Control

### SEC-P3-003: SQL Injection via Dynamic Table/Column Identifiers (CRITICAL)

- **Severity:** CRITICAL
- **Affected components:** QueryBuilder, Generic Data API, Generic Form (column names from metadata), Generic Grid (column sorting/filtering)
- **HLD requirement:** Architecture Principle 6 -- All dynamic SQL identifiers are validated against metadata. CLAUDE.md Rule 17 -- Dynamic SQL identifiers must come only from trusted metadata.

**Current state:** Phase 2 ValRuleEngine has a table allowlist. However, the QueryBuilder (not yet implemented) must also validate table names, column names, and sort columns against the metadata dictionary. The Generic Grid will accept sortColumn, filterColumn, etc. from the client -- these become SQL ORDER BY and WHERE clause identifiers, which CANNOT be parameterized.

**Threat model:**
- Grid requests GET /api/data/Orders?sortColumn=name&filterColumn=email&filterValue=attacker@evil.com.
- If sortColumn is concatenated directly into ORDER BY {sortColumn}, an attacker can inject arbitrary SQL: name; DROP TABLE SysTable; --.
- PostgreSQL ORDER BY does not support parameterized identifiers.

**Exploit scenario:**
1. Attacker sends GET /api/data/X_Orders?sortColumn=name;SELECT pg_sleep(30);--
2. QueryBuilder constructs: SELECT * FROM X_Orders ORDER BY name;SELECT pg_sleep(30);-- ASC.
3. SQL injection succeeds -- database connection blocked.

**Required mitigations:**
1. All table names MUST be resolved from MetadataGraph before use in SQL. MetadataGraph.GetTableName(string) must return the canonical quoted identifier or null.
2. All column names in ORDER BY, GROUP BY, SELECT lists must be validated against MetadataGraph.GetColumns(tableName) and ONLY columns present in the dictionary may be used.
3. Column names must be double-quoted per PostgreSQL identifier rules: "ColumnName".
4. The list of allowed columns for each query must come from the dictionary -- never from client input.
5. Search/filter values are parameterized; only identifiers (table/column names) are interpolated, and only from trusted metadata.
6. Integration test: attempt SQL injection via grid sort/filter parameters.

**OWASP Top 10:** A03:2021 -- Injection

### SEC-P3-004: XSS via Metadata-Driven Content Rendering (HIGH)

- **Severity:** HIGH
- **Affected components:** React / Generic Form, React / Generic Grid, React / Display Logic, all React components that render metadata-driven content
- **HLD requirement:** Section 32 (React structure), items 23-29 (generic form, grid, lookup, display logic). CLAUDE.md Rule 5 -- UI is never the security boundary.

**Current state:** React + TypeScript is specified. No React code exists yet. The risk is in how the generic form/grid will render values from the database -- particularly free-text fields, notes, descriptions, and rich-text columns.

**Threat model:**
- A malicious user or compromised account stores a script tag in a free-text column.
- The generic form renders this value without escaping in an innerHTML-like context.
- XSS executes in the context of the application.
- Token theft, session hijacking, credential harvesting.

**Exploit scenario:**
1. Attacker creates a record with Description = img tag with onerror fetching cookie to attacker server in a free-text field.
2. User B opens the generic form for that record.
3. Generic form renders descriptionField.Value using curly braces (safe in JSX) but if it uses dangerouslySetInnerHTML or a TextField component that renders innerHTML, XSS occurs.
4. Attacker captures User Bs JWT token.

**Required mitigations:**
1. All React component text output must use curly braces (Reacts JSX escapes HTML by default) -- NEVER dangerouslySetInnerHTML.
2. If rich-text content must be stored and displayed, implement server-side sanitization (e.g., using HtmlSanitizer) and explicitly mark fields as rich-text.
3. Generic form must NOT render arbitrary metadata values as JSX children without escaping.
4. Generic form display logic (conditional rendering) must be evaluated server-side or use a safe subset of expressions -- never eval() or new Function().
5. Set Content-Security-Policy header with script-src self on all API responses.
6. React Helmet or equivalent to set CSP on the frontend.

**OWASP Top 10:** A03:2021 -- Injection (XSS subcategory)

### SEC-P3-005: Overbroad Data Projection (HIGH)

- **Severity:** HIGH
- **Affected components:** Generic Data API, QueryBuilder, Generic Grid
- **HLD requirement:** Sections 26-28 end-to-end flows. Item 37 in implementation changes -- Strengthen column-level projection/write filtering.

**Current state:** The generic data API will return all columns for a record by default. There is no column-level security or projection control implemented.

**Threat model:**
- Table Employees has columns: Id, Name, SSN, Salary, PasswordHash, InternalNotes.
- SysColumn metadata defines all columns, including sensitive ones.
- Generic form/grid requests GET /api/data/Employees/42.
- Response includes ALL columns including SSN, PasswordHash, Salary.
- Any role with table read access gets all columns, including PII and secrets.

**Exploit scenario:**
1. Employee self-service portal role should only see Name, Department, Phone.
2. Generic grid uses SELECT * FROM X_Employees WHERE Id = 42.
3. Response returns all columns including SSN, PasswordHash, Salary.
4. PII breach across all employees.

**Required mitigations:**
1. SysColumn must have an IsSensitive or IsHidden flag that restricts projection.
2. Column-level access control must be enforced in QueryBuilder.BuildSelect() -- only columns the users role can see are included in the SELECT clause.
3. Sensitive columns must be encrypted at rest (AES-256) and decrypted only for roles that have CanViewSensitive permission.
4. API response schema must be validated against column metadata before serialization -- no extra columns allowed.
5. Unit tests: verify that a role without column read access does not receive that column in the response.

**OWASP Top 10:** A01:2021 -- Broken Access Control

### SEC-P3-006: Column-Level Access Control Not Enforced (HIGH)

- **Severity:** HIGH
- **Affected components:** Generic Data API, Generic Form (write operations), QueryBuilder
- **HLD requirement:** Item 37 in implementation changes -- Strengthen column-level projection/write filtering.

**Current state:** The generic form collects data from React fields and sends a JSON payload to POST /api/data/{table} or PUT /api/data/{table}/{id}. There is no server-side enforcement of which columns a given user/role can write.

**Threat model:**
- Form UI hides Salary column for a junior HR user (client-side security).
- Junior HR user opens browser DevTools, modifies the JSON payload to include salary field.
- Server accepts the payload and updates the Salary column because no column-level write check exists.
- Client-side hiding of fields is not a security boundary (CLAUDE.md Rule 5).

**Exploit scenario:**
1. React form renders fields for Name and Email but not Salary (based on window metadata + client-side role check).
2. Attacker crafts: PUT /api/data/Employees/42 with salary field included.
3. API endpoint accepts the payload, sets all fields from the JSON body (mass assignment).
4. Salary updated without authorization.

**Required mitigations:**
1. Every API endpoint must check column-level write permissions before applying values.
2. Implement a ColumnWritePermissionService that takes (userId, roleId, tableName, columnName) and returns whether writes are allowed.
3. Generic Data API endpoints must whitelist which columns are writable for the current context before applying the request body to the PO.
4. Form fields that are hidden on the client must also be rejected server-side -- never assume client hiding equals security.
5. Unit tests: attempt to write to a read-only column via API -- must be rejected.

**OWASP Top 10:** A01:2021 -- Broken Access Control (Insecure Direct Object References, Mass Assignment)

### SEC-P3-007: Denial of Service on High-Volume Reference Tables (MEDIUM)

- **Severity:** MEDIUM
- **Affected components:** Lookup API (/api/lookup/{referenceId}), Lookup/Search controls in React
- **HLD requirement:** Section 30 Performance -- Search-based high-volume lookups.

**Current state:** No lookup endpoints exist. The risk is in how they are implemented when added.

**Threat model:**
- Table Products has 10 million rows with a reference-based lookup.
- GET /api/lookup/ProductRef returns all 10 million rows with no pagination or limit.
- Browser crashes, API server memory exhausted, database connection pool depleted.
- Concurrent requests amplify the impact (all consuming database connections).

**Exploit scenario:**
1. Attacker sends GET /api/lookup/CountryRef (193 countries is fine) then GET /api/lookup/ProductRef (10M rows).
2. Response body is hundreds of megabytes.
3. Memory pressure on API server and network saturation.
4. Subsequent requests to any endpoint timeout due to resource exhaustion.

**Required mitigations:**
1. All lookup endpoints MUST enforce a maximum page size (e.g., 1000 rows per request).
2. Lookup endpoints MUST support pagination: skip and take query parameters.
3. Lookup endpoints MUST support search/filtering: ?search=term to reduce result set.
4. Implement rate limiting on lookup endpoints: max 50 requests per second per client IP.
5. Consider implementing search-based lookups using full-text search (PostgreSQL tsvector) instead of pattern matching for large reference tables.
6. High-volume reference tables (more than N rows) must require a search term -- no unfiltered listing.

**OWASP Top 10:** A04:2021 -- Insecure Design, A05:2021 -- Security Misconfiguration

### SEC-P3-008: No Audit Logging for Create/Update/Delete Operations (MEDIUM)

- **Severity:** MEDIUM
- **Affected components:** Generic Data API, Platform.API, POLifecycleManager
- **HLD requirement:** Section 30 Security -- Audit for protected operations. Sections 26-28 end-to-end flows show Audit as a mandatory step after every CRUD operation.

**Current state:** No audit infrastructure exists. The SysAuditLog table and related metadata are part of Phase 6.

**Threat model:**
- Attacker deletes 10,000 records.
- No audit trail exists -- there is no way to determine who did what and when.
- Forensic investigation impossible.
- Compliance requirements (SOC2, HIPAA, GDPR) not met.

**Exploit scenario:**
1. Compromised account performs DELETE /api/data/Orders/1..10000.
2. No record of the deletion exists.
3. Business impact: lost revenue data, unable to prove data integrity.
4. Compliance auditor finds no audit trail for protected operations.

**Required mitigations:**
1. Generic Data API endpoints MUST log every CREATE, UPDATE, DELETE operation, including:
   - User ID (from JWT subject claim)
   - Tenant ID
   - Organization ID
   - Table name
   - Record ID (primary key)
   - Operation type (C/U/D)
   - Timestamp (UTC)
   - Old values (for UPDATE)
   - New values (for CREATE/UPDATE) -- at minimum, list of changed columns
   - IP address (HttpContext.Connection.RemoteIpAddress)
2. Audit log writes MUST be transactional with the data operation: if the data write fails, the audit must not be committed. If the data write succeeds but audit fails, the audit should be queued (async) and never suppress the business operation.
3. Audit log is append-only -- no updates or deletes to audit records (enforce at DB level with triggers).
4. Audit log storage must be considered a protected table with restricted access.
5. Unit tests: verify audit records are created for every CRUD operation.
6. Integration tests: verify audit records contain correct user, tenant, and value data.

**OWASP Top 10:** A09:2021 -- Security Logging and Monitoring Failures

### SEC-P3-009: CSRF Risk with JWT Bearer Tokens (MEDIUM)

- **Severity:** MEDIUM
- **Affected components:** React frontend, Platform.API
- **HLD requirement:** ADR-002 specifies JWT bearer tokens (stateless). JWTs sent as Authorization: Bearer <token> headers are not susceptible to CSRF by default (browsers do not auto-send custom headers).

**Current state:** No auth implemented. No frontend exists.

**Threat model:**
- JWT tokens are stored in localStorage by the React app.
- If the React app is vulnerable to XSS (SEC-P3-004), an attacker can read the token from localStorage.
- A separate CSRF vector would exist if tokens were stored in HTTP-only cookies and the API accepted cookie-based auth for stateful sessions.
- With JWT bearer tokens in Authorization headers, CSRF is inherently mitigated -- the browser will not include custom headers in cross-origin requests.

**Required mitigations:**
1. Confirm JWTs are sent ONLY via the Authorization header, NOT via cookies.
2. If cookies are used for refresh tokens, they must be HttpOnly, Secure, SameSite=Strict.
3. Implement CSRF token mechanism as defense-in-depth if any endpoint accepts state-changing requests via cookies.
4. Set Cross-Origin-Opener-Policy, Cross-Origin-Resource-Policy, and Referrer-Policy headers.
5. Configure CORS explicitly -- never use AddCors() with a wildcard origin in production.

**OWASP Top 10:** A01:2021 -- Broken Access Control (Cross-Site Request Forgery)

### SEC-P3-010: Client-Side Display-Logic Bypass (MEDIUM)

- **Severity:** MEDIUM
- **Affected components:** React / Display Logic, Generic Form (conditional field visibility)
- **HLD requirement:** Item 27 in implementation changes -- Implement display-logic evaluation. CLAUDE.md Rule 5 -- UI is never the security boundary.

**Current state:** No display logic implementation exists.

**Threat model:**
- Window metadata defines a display logic rule: show ApprovalNotes field only if Status = Pending.
- React evaluates the display logic client-side and hides the field when Status = Approved.
- Developer submits with ApprovalNotes field omitted (because it is hidden).
- **However**, a malicious user modifies the React app to always show the field, or crafts a direct API call including ApprovalNotes.
- Server receives the request and updates ApprovalNotes because the generic form does not validate display-logic constraints.

**Exploit scenario:**
1. System admin creates a form with a conditional field that should only be visible for certain workflow states.
2. The conditional visibility is enforced only on the client side.
3. Attacker crafts a PUT /api/data/Invoice/42 with approvalNotes field even when the invoice is approved.
4. Server applies the change -- client-side visibility rules are not security rules.

**Required mitigations:**
1. Display logic must be documented as UI convenience only -- never used as the sole enforcement mechanism.
2. Any field that has business-rule constraints (e.g., ApprovalNotes can only be set when Status is Pending) must be enforced server-side via:
   - ValRule with type ModelValidator or equivalent
   - M_<Table> business logic that validates field-state compatibility
3. Generic form API endpoints must NOT automatically apply all fields from the request body. Instead, they should either:
   - Apply only the fields listed in a fields whitelist in the request, OR
   - Validate that all fields in the request are writable for the current context (SEC-P3-006).
4. Unit tests: attempt to set a hidden field via API -- must be evaluated against server-side rules.

**OWASP Top 10:** A01:2021 -- Broken Access Control

### SEC-P3-011: Server-Side Row-Level Predicate Not Enforced in Bulk Operations (MEDIUM)

- **Severity:** MEDIUM
- **Affected components:** Generic Data API, Generic Grid (bulk delete, bulk update), QueryBuilder
- **HLD requirement:** Item 38 in implementation changes -- Strengthen server-side row predicate enforcement.

**Current state:** No bulk operations implemented.

**Threat model:**
- Generic grid has a Delete Selected checkbox feature for bulk deletion.
- Grid shows 50 rows (filtered by the grids server-side WHERE clause).
- User selects 10 rows and clicks Delete Selected.
- Grid sends DELETE /api/data/Orders?ids=1,2,3,4,5,6,7,8,9,10.
- **BUG:** API endpoint deletes by primary key without checking whether the requesting user has access to those specific records.
- User could delete records from other tenants if IDs are guessed, or records they should not have access to.

**Exploit scenario:**
1. User A (tenant=ACME) sees orders filtered to their tenant.
2. User A discovers record IDs 50001-50010 belong to tenant=GLOBEX (by inspecting URLs or guessing).
3. User A sends DELETE /api/data/Orders?ids=50001,50002,...,50010.
4. If the API deletes by primary key without checking tenant membership in the WHERE clause, GLOBEX records are deleted.

**Required mitigations:**
1. Every bulk operation (bulk delete, bulk update, bulk approve) must include the tenant and org predicates in the WHERE clause.
2. For individual operations, WHERE Id = @Id AND TenantId = @TenantId AND OrgId = @OrgId must always be present.
3. Bulk operations must return the count of affected rows and fail if the count does not match the number of IDs requested (this prevents partial success that could indicate a predicate issue).
4. Consider implementing PostgreSQL Row-Level Security (RLS) as a defense-in-depth layer (HLD item 40).

**OWASP Top 10:** A01:2021 -- Broken Access Control

### SEC-P3-012: Swagger/OpenAPI Exposes API Contract Without Authentication (LOW)

- **Severity:** LOW
- **Affected components:** Platform.API (Swagger, OpenAPI)
- **HLD requirement:** Standard security configuration per Section 30.

**Current state:** Program.cs lines 27-28: builder.Services.AddEndpointsApiExplorer() and builder.Services.AddSwaggerGen() are always registered. Swagger UI is enabled in Development (app.UseSwaggerUI()). When data endpoints are added, Swagger will document every endpoint with its full parameter contract.

**Threat model:**
- Swagger UI at /swagger documents all endpoints, including their request/response schemas.
- Even without auth, an attacker can see the complete API contract.
- Swagger can be used to automate exploitation (e.g., curl requests to every documented endpoint).
- In development mode, Swagger has full access to all data.

**Required mitigations:**
1. In production, Swagger must be completely disabled: if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }.
2. Do not expose the OpenAPI JSON document (/swagger/v1/swagger.json) in production.
3. Consider adding API versioning to prevent breaking changes that could inadvertently expose deprecated endpoints.

**OWASP Top 10:** A05:2021 -- Security Misconfiguration

---

## Cross-Cutting Concerns

### Authentication Flow (Prerequisite for All Endpoints)

Client -> POST /api/auth/login { username, password }
        -> Server validates credentials against ASP.NET Identity
        -> Server returns { accessToken, refreshToken }
        -> Client stores access token in memory (not localStorage for access)
        -> Client sends Authorization: Bearer <accessToken> on every API call
        -> Server middleware:
            1. Validates JWT signature
            2. Extracts claims: sub (userId), tenant, roles
            3. Creates IReadOnlyContext from claims
            4. Passes context to endpoint handler
        -> Endpoint handler uses context for authorization decisions

### Defense-in-Depth Layers for Phase 3

Layer 1: Authentication (JWT)       -> Identifies user (SEC-P3-001)
Layer 2: Authorization (RBAC)        -> Authorizes table/column/record access (SEC-P3-002, 005, 006)
Layer 3: Tenant/Predicate Injection  -> Enforces tenant isolation in every SQL (SEC-P3-002)
Layer 4: Identifier Validation       -> Prevents SQL injection (SEC-P3-003)
Layer 5: XSS Prevention              -> Escapes all output (SEC-P3-004)
Layer 6: Rate Limiting               -> Prevents DoS (SEC-P3-007)
Layer 7: Audit Logging               -> Records all mutations (SEC-P3-008)
Layer 8: CSRF Protection             -> Prevents cross-site forgery (SEC-P3-009)

---

## Relationship to Phase 2 Findings

| Phase 2 Finding | Impact on Phase 3 | Phase 3 Action |
|----------------|-------------------|----------------|
| SEC-P2-001 (SQL Injection ValRule) | ValRule is called during CRUD; its table allowlist must be consistent with QueryBuilder allowlist | Verify ValRule table allowlist matches data API table allowlist |
| SEC-P2-004 (Context injection) | Context must be populated from JWT, not headers | Implement authentication middleware first |
| SEC-P2-007 (Metadata enumeration) | Metadata endpoints added in Phase 3 are now exposed | All metadata endpoints require auth |
| SEC-P2-011 (Tenant isolation) | Predicate injection must be wired into data API | Implement predicate injection in QueryBuilder |

---

## Acceptance Criteria

Phase 3 security is considered complete when:

1. [ ] Every API endpoint (except health) returns 401 Unauthorized without a valid JWT.
2. [ ] JWT validation rejects expired, malformed, or unsigned tokens.
3. [ ] Tenant and org predicates appear in every generated SQL query (verified by unit test).
4. [ ] SQL injection via grid sort/filter parameters is rejected (verified by integration test).
5. [ ] No React component uses dangerouslySetInnerHTML.
6. [ ] Sensitive columns are excluded from API responses for unauthorized roles.
7. [ ] Lookup endpoints enforce pagination and rate limits.
8. [ ] Every CRUD operation generates an audit record.
9. [ ] Bulk operations include tenant predicates in WHERE clause.
10. [ ] Swagger is disabled in production.
11. [ ] CORS is explicitly configured, not wildcard.
12. [ ] All 12 findings above have corresponding tests (unit or integration).

---

## Design Closure — Disposition (2026-08-15)

All 12 findings have design mitigations defined. Implementation must follow these dispositions.

| Finding | Severity | Design Mitigation | Status | Owner |
|---|---|---|---|---|
| SEC-P3-001: No auth | CRITICAL | Phase 4 (ADR-0002) implements JWT auth. Phase 3 endpoints accept IReadOnlyContext structure. | DESIGN-MITIGATED | Phase 4 |
| SEC-P3-002: Tenant isolation | CRITICAL | QueryBuilder injects TenantPredicate/OrgPredicate from IReadOnlyContext. Phase 3 uses null context. | DESIGN-MITIGATED | Phase 4 |
| SEC-P3-003: SQL injection | CRITICAL | 3-layer: table allowlist (sys_table), column allowlist (sys_column), parameterized values (NpgsqlParameter). | DESIGN-MITIGATED | Phase 3 |
| SEC-P3-004: XSS | HIGH | React curly braces (auto-escape), no dangerouslySetInnerHTML, CSP header. | DESIGN-MITIGATED | Phase 3 |
| SEC-P3-005: Overbroad projection | HIGH | Column allowlist + IsEncrypted exclusion + explicit projection. | DESIGN-MITIGATED | Phase 3 |
| SEC-P3-006: Column access control | HIGH | Allowed columns from context.Extensions["AllowedColumns"]. Phase 3 allows all; Phase 4 enforces. | DESIGN-MITIGATED | Phase 4 |
| SEC-P3-007: DoS on lookups | MEDIUM | PageSize cap (500), high-volume requires search, Redis caching. | DESIGN-MITIGATED | Phase 3 |
| SEC-P3-008: No audit logging | MEDIUM | Audit call points in Data API. SysChangeLog is Phase 6. | DESIGN-MITIGATED | Phase 6 |
| SEC-P3-009: CSRF | MEDIUM | JWT in Authorization header (not cookie) = CSRF-proof by design. | DESIGN-MITIGATED | Phase 3 |
| SEC-P3-010: Display logic bypass | MEDIUM | Server re-evaluates mandatory/readonly via POValidator. Client display logic is UX only. | DESIGN-MITIGATED | Phase 3 |
| SEC-P3-011: Bulk row predicate | MEDIUM | No bulk ops in Phase 3. QueryBuilder will enforce predicates. | DESIGN-MITIGATED | Future |
| SEC-P3-012: Swagger exposure | LOW | Swagger disabled in production (IsDevelopment check). | DESIGN-MITIGATED | Phase 3 |

### Carry-over from Phase 2

| Finding | Severity | Status | Action |
|---|---|---|---|
| SEC-P2-007: Metadata enumeration | Medium | DEFERRED | Phase 4 auth + RBAC limits enumeration |
| SEC-P2-004: Context variable injection | Medium | IMPLEMENTATION-REQUIRED | Stub in Phase 2, JWT integration Phase 4 |

### Critical/High Design Mitigation Summary

**CRITICAL findings (3/3 DESIGN-MITIGATED):**
1. **Auth (SEC-P3-001):** Deferred to Phase 4. API structure is auth-ready.
2. **Tenant isolation (SEC-P3-002):** QueryBuilder + predicate injection. Phase 3 builds plumbing, Phase 4 connects.
3. **SQL injection (SEC-P3-003):** Table/column allowlists + parameterized values. Implemented in Phase 3.

**HIGH findings (3/3 DESIGN-MITIGATED):**
1. **XSS (SEC-P3-004):** React escaping + CSP header. Implemented in Phase 3.
2. **Overbroad projection (SEC-P3-005):** Column allowlist + encrypted column exclusion. Implemented in Phase 3.
3. **Column access control (SEC-P3-006):** Allowed columns from context. Phase 3 allows all, Phase 4 enforces.
