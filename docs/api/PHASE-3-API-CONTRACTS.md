# Phase 3 — API Contracts

**Phase:** 3 — UI (Generic Forms, Grids, Lookups, Menus)
**Date:** 2026-08-15
**Status:** Design — awaiting implementation

---

## 1. API Overview

Phase 3 introduces 3 new API groups on top of the existing `/health` endpoint:

| Group | Base Route | Purpose |
|---|---|---|
| Window Metadata | `/api/meta/window/{windowId}` | Return UI metadata for a window |
| Window List | `/api/meta/windows` | List all available windows |
| Menu | `/api/meta/menu` | Return navigation menu hierarchy |
| Data CRUD | `/api/data/{table}` | Generic CRUD for any registered table |
| Data Single | `/api/data/{table}/{id}` | Get/update/delete single record |
| Lookup | `/api/lookup/{referenceId}` | Return key-value pairs for references |

**Auth boundary:** All endpoints accept optional `Authorization: Bearer <token>` header.
Authentication is implemented in Phase 4. In Phase 3, all requests are treated as anonymous with `IReadOnlyContext` containing null values for UserId, TenantId, and OrgId. The API structure MUST accept context — no shortcuts.

---

## 2. Window Metadata API

### 2.1 GET /api/meta/window/{windowId}

Returns the complete UI metadata for a window, including tabs, fields, field groups, and display logic.

**Request:**
```
GET /api/meta/window/100
Authorization: Bearer <token>  -- optional in Phase 3
```

**Path Parameters:**
| Parameter | Type | Required | Description |
|---|---|---|---|
| windowId | int | Yes | Window ID from sys_window |

**Response — 200 OK:**
```json
{
  "windowId": 100,
  "columnName": "window_library_book",
  "name": "Library Book",
  "description": "Manage library book records",
  "tabs": [
    {
      "tabId": 1,
      "columnName": "main",
      "name": "Main",
      "table": "library_book",
      "isGrid": false,
      "fields": [
        {
          "columnName": "Title",
          "label": "Title",
          "help": "Book title",
          "controlType": "TextInput",
          "isMandatory": true,
          "isReadOnly": false,
          "isMandatoryOverride": false,
          "isReadOnlyOverride": false,
          "colSpan": 1,
          "rowSpan": 1,
          "defaultValue": null,
          "displayLogic": null,
          "readOnlyLogic": null,
          "mandatoryLogic": null,
          "fieldGroup": null,
          "sysReference": {
            "name": "vChar",
            "validationType": "baseType",
            "valueFormat": "VARCHAR"
          },
          "fieldLength": 120
        },
        {
          "columnName": "IsAvailable",
          "label": "Available",
          "help": null,
          "controlType": "YesNoToggle",
          "isMandatory": false,
          "isReadOnly": false,
          "isMandatoryOverride": false,
          "isReadOnlyOverride": false,
          "colSpan": 1,
          "rowSpan": 1,
          "defaultValue": "true",
          "displayLogic": null,
          "readOnlyLogic": null,
          "mandatoryLogic": null,
          "fieldGroup": "Availability",
          "sysReference": {
            "name": "yesNo",
            "validationType": "baseType",
            "valueFormat": "BOOLEAN"
          },
          "fieldLength": null
        }
      ],
      "fieldGroups": [
        {
          "groupName": "Availability",
          "label": "Availability",
          "colSpan": 1,
          "isCollapsed": false,
          "fields": ["IsAvailable", "AvailableDate"]
        }
      ]
    }
  ]
}
```

**Response — 404 Not Found:**
```json
{
  "error": {
    "code": "WindowNotFound",
    "message": "Window with ID 999 not found."
  }
}
```

**Response — 403 Forbidden (Phase 4):**
```json
{
  "error": {
    "code": "Unauthorized",
    "message": "You do not have access to this window."
  }
}
```

**Caching:** Window metadata is cached in IMemoryCache + Redis (key: `window:{windowId}`).
Invalidation: DictionaryChangedEvent triggers cache invalidation (Phase 2 infrastructure).

**Audit:** No audit log entry needed (metadata read, not data mutation).

---

### 2.2 GET /api/meta/windows

Returns a list of all windows the user can access.

**Request:**
```
GET /api/meta/windows
```

**Response — 200 OK:**
```json
{
  "windows": [
    {
      "windowId": 100,
      "columnName": "window_library_book",
      "name": "Library Book",
      "description": "Manage library book records"
    }
  ]
}
```

**Filtering (optional, Phase 4):**
```
GET /api/meta/windows?isActive=true
```

---

## 3. Menu API

### 3.1 GET /api/meta/menu

Returns the navigation menu hierarchy.

**Request:**
```
GET /api/meta/menu
```

**Response — 200 OK:**
```json
{
  "items": [
    {
      "menuId": 1,
      "columnName": "menu_books",
      "name": "Books",
      "icon": "BookOutlined",
      "sequence": 10,
      "parentId": null,
      "children": [
        {
          "menuId": 2,
          "columnName": "menu_books_list",
          "name": "Book List",
          "icon": "ListOutlined",
          "sequence": 10,
          "parentId": 1,
          "windowId": 100,
          "processId": null,
          "isSeparator": false,
          "children": []
        },
        {
          "menuId": 3,
          "columnName": "menu_books_separator",
          "name": "",
          "icon": null,
          "sequence": 20,
          "parentId": 1,
          "windowId": null,
          "processId": null,
          "isSeparator": true,
          "children": []
        }
      ]
    }
  ]
}
```

**Response — 204 No Content:** Empty menu (no active menu items).

**Filtering (Phase 4):** `?role=admin` — filter menu by role visibility.

**Caching:** Cached in IMemoryCache + Redis (key: `menu:root`).

---

## 4. Data API

### 4.1 GET /api/data/{table}

Returns a paginated list of records for the given table.

**Request:**
```
GET /api/data/library_book?page=1&pageSize=50&sortBy=Name&sortDir=asc&filter={"conjunction":"and","filters":[...]}
```

**Path Parameters:**
| Parameter | Type | Required | Description |
|---|---|---|---|
| table | string | Yes | Table name (must be in sys_table.TableName allowlist) |

**Query Parameters:**
| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| page | int | No | 1 | Page number (1-based) |
| pageSize | int | No | 50 | Records per page (1-500) |
| sortBy | string | No | — | Column to sort by (must be in sys_column allowlist) |
| sortDir | string | No | asc | Sort direction: `asc` or `desc` |
| filter | string | No | — | JSON filter AST (URL-encoded) |
| columns | string | No | — | Comma-separated column names to project (empty = all visible) |

**Response — 200 OK:**
```json
{
  "items": [
    {
      "libraryBookId": 1,
      "title": "The Pragmatic Programmer",
      "isbn": "978-0-13-468599-1",
      "isAvailable": true,
      "availableDate": "2024-01-15"
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 50,
    "totalItems": 1234,
    "totalPages": 25
  }
}
```

**Validation:**
1. Table name validated against `sys_table.TableName` → 400 if not found
2. `sortBy` column validated against `sys_column.ColumnName` for the given table → 400 if invalid
3. `pageSize` clamped to [1, 500] → 400 if exceeded
4. Filter AST parsed and validated → 400 on parse/validation error

**Tenant/Org isolation (Phase 4):** Tenant and org predicates injected via `IReadOnlyContext`. In Phase 3, predicates are null (no isolation applied).

**Audit:** Read operation — no audit log entry.

---

### 4.2 GET /api/data/{table}/{id}

Returns a single record.

**Request:**
```
GET /api/data/library_book/1
```

**Response — 200 OK:**
```json
{
  "libraryBookId": 1,
  "title": "The Pragmatic Programmer",
  "isbn": "978-0-13-468599-1",
  "isAvailable": true,
  "availableDate": "2024-01-15"
}
```

**Response — 404 Not Found:**
```json
{
  "error": {
    "code": "RecordNotFound",
    "message": "Record 1 not found in table library_book."
  }
}
```

---

### 4.3 POST /api/data/{table}

Creates a new record.

**Request:**
```
POST /api/data/library_book
Content-Type: application/json

{
  "title": "The Pragmatic Programmer",
  "isbn": "978-0-13-468599-1",
  "isAvailable": true
}
```

**Response — 201 Created:**
```json
{
  "libraryBookId": 2,
  "title": "The Pragmatic Programmer",
  "isbn": "978-0-13-468599-1",
  "isAvailable": true,
  "availableDate": null
}
```

**Response — 400 Validation Error:**
```json
{
  "error": {
    "code": "ValidationFailed",
    "message": "Validation failed for 2 fields.",
    "details": [
      {
        "field": "Title",
        "columnName": "title",
        "rule": "mandatory",
        "message": "Title is mandatory."
      },
      {
        "field": "IsAvailable",
        "columnName": "is_available",
        "rule": "type",
        "message": "Expected boolean, got string."
      }
    ]
  }
}
```

**Validation pipeline:**
1. Table name validated against allowlist → 400
2. Request body parsed as JSON → 400 on parse error
3. Field names validated against sys_column for the table → 400 on unknown field
4. Excluded columns (sysTableId, createdAt, etc.) → silently removed
5. POValidator runs: mandatory → type → length → reference → valrule
6. POLifecycleManager.Create() called
7. After-save hooks executed

**Audit:** INSERT logged to SysChangeLog (Phase 6).

---

### 4.4 PUT /api/data/{table}/{id}

Updates an existing record.

**Request:**
```
PUT /api/data/library_book/1
Content-Type: application/json

{
  "title": "The Pragmatic Programmer (2nd Edition)"
}
```

**Response — 200 OK:** Updated record.

**Response — 404 Not Found:** Record not found.

**Response — 409 Conflict:**
```json
{
  "error": {
    "code": "ConcurrencyConflict",
    "message": "Record was modified by another user."
  }
}
```

**Validation pipeline:** Same as POST, plus:
1. Load existing record → 404 if not found
2. Check IsUpdateable per column → 400 on immutable column
3. Track changed fields for audit log

**Audit:** UPDATE logged to SysChangeLog with old/new values.

---

### 4.5 DELETE /api/data/{table}/{id}

Deletes a record.

**Request:**
```
DELETE /api/data/library_book/1
```

**Response — 204 No Content:** Deleted successfully.

**Response — 404 Not Found:** Record not found.

**Response — 409 Conflict:**
```json
{
  "error": {
    "code": "DependencyConflict",
    "message": "Cannot delete: record is referenced by other data."
  }
}
```

**Validation:**
1. Table IsDeleteable checked → 400 if false
2. FK dependency check → 409 if referenced
3. M_<Table>.Delete() business rules

**Audit:** DELETE logged to SysChangeLog.

---

## 5. Lookup API

### 5.1 GET /api/lookup/{referenceId}

Returns key-value pairs for a reference (dropdown, lookup, or search).

**Request:**
```
GET /api/lookup/1?search=pragmatic&page=1&pageSize=50
```

**Path Parameters:**
| Parameter | Type | Required | Description |
|---|---|---|---|
| referenceId | int | Yes | SysReference_ID from database |

**Query Parameters:**
| Parameter | Type | Required | Description |
|---|---|---|---|
| search | string | No | Search text for partial matching |
| page | int | No | 1 | Page number |
| pageSize | int | No | 50 | Records per page (1-500) |

**Response — 200 OK (LIST reference):**
```json
{
  "referenceId": 1,
  "referenceName": "yesNo",
  "items": [
    { "key": "Y", "displayValue": "Yes" },
    { "key": "N", "displayValue": "No" }
  ]
}
```

**Response — 200 OK (TABLE reference with search):**
```json
{
  "referenceId": 5,
  "referenceName": "author_lookup",
  "items": [
    { "key": 42, "displayValue": "David Thomas" }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 50,
    "totalItems": 1,
    "totalPages": 1
  }
}
```

**Response — 404 Not Found:**
```json
{
  "error": {
    "code": "ReferenceNotFound",
    "message": "Reference with ID 999 not found."
  }
}
```

**Validation:**
1. Reference validated against sys_reference → 404
2. ValidationType checked → LIST loads from sys_reference_list, TABLE loads from referenced table, SEARCH uses whereClause
3. For TABLE/SEARCH: table name validated against allowlist
4. For TABLE/SEARCH: search text applied via LIKE with parameterized query
5. Pagination clamped to [1, 500]

**High-volume handling:** If sys_table.IsHighVolume = TRUE, pagination is required (pageSize default = 20, max = 100).

**Audit:** Read operation — no audit log.

---

## 6. Error Response Format

All errors use a consistent format:

```json
{
  "error": {
    "code": "ErrorCode",
    "message": "Human-readable message.",
    "details": []
  }
}
```

**Standard error codes:**

| Code | HTTP Status | Description |
|---|---|---|
| WindowNotFound | 404 | Window ID not found |
| RecordNotFound | 404 | Data record not found |
| ReferenceNotFound | 404 | Lookup reference not found |
| TableNotAllowed | 400 | Table name not in allowlist |
| ValidationFailed | 400 | Field validation errors |
| Unauthorized | 403 | Access denied (Phase 4) |
| ConcurrencyConflict | 409 | Optimistic locking conflict |
| DependencyConflict | 409 | FK reference prevents delete |
| InternalError | 500 | Unexpected server error |

---

## 7. Caching Strategy

| Endpoint | Cache Key | TTL | Invalidation |
|---|---|---|---|
| GET /api/meta/window/{id} | `window:{id}` | 5 min (cache) / permanent (memory) | DictionaryChangedEvent |
| GET /api/meta/windows | `windows:list` | 5 min | DictionaryChangedEvent |
| GET /api/meta/menu | `menu:root` | 5 min | DictionaryChangedEvent |
| GET /api/data/{table} | — | None (data is volatile) | N/A |
| GET /api/data/{table}/{id} | `data:{table}:{id}` | 1 min | DictionaryChangedEvent + data mutation |
| POST/PUT/DELETE /api/data | — | None | Invalidates related caches |
| GET /api/lookup/{id} | `lookup:{id}` | 5 min | DictionaryChangedEvent + data mutation |

Cache invalidation uses Phase 2 infrastructure (CacheInvalidationService with Redis pub/sub).

---

## 8. IReadOnlyContext Propagation

### Phase 3 (no auth yet)

```
HTTP Request
    ↓
No auth middleware (Phase 4)
    ↓
IReadOnlyContext = InMemoryContext.Create(null, null, null)
    ↓
All context values = null
    ↓
Services receive context but skip tenant/org filtering
```

### Phase 4 readiness

```
HTTP Request
    ↓
Auth middleware validates JWT
    ↓
Claims extracted: sub=UserId, tenant=TenantId, org=OrgId
    ↓
IReadOnlyContext = InMemoryContext.Create(userId, tenantId, orgId)
    ↓
TenantPredicate = "tenant_id = @TenantId"
OrgPredicate = "org_id = @OrgId"
    ↓
QueryBuilder injects predicates into WHERE clause
    ↓
Parameterized SQL with tenant/org isolation
```

### Context propagation pattern

```csharp
// In API endpoint:
var context = HttpContext.Items["Context"] as IReadOnlyContext
    ?? InMemoryContext.Create(null, null, null);

// Pass to service:
var result = await _dataService.ListAsync(table, context, pagination, filter);
```

### What happens when context values are missing

| Missing Value | Behavior in Phase 3 | Behavior in Phase 4 |
|---|---|---|
| UserId | Ignored (no audit user) | Logged to audit trail |
| TenantId | No tenant filtering | Tenant predicate injected |
| OrgId | No org filtering | Org predicate injected |
| Role | Not used | Role-based access control |

No silent defaults. Null context values = no filtering in Phase 3. Phase 4 will populate all values.
