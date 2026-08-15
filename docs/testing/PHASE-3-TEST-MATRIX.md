# Phase 3 — UI (Generic Forms, Grids, Lookups, Menus): Test Matrix

**Phase:** 3 (UI)
**Status:** Design
**Based on:** HLD/LLD Sections 8 (UI Metadata), 9 (React Metadata Contract), 13 (Generic API), 14 (Lookup Architecture), 34 (Migration Plan), 35 (Implementation Items 21-30), 36 (Acceptance Criteria 6-8)

## Phase 3 Scope

| Implementation Item | Description | Acceptance Criteria |
|---|---|---|
| 21 | Standardize React/TypeScript | N/A (config/linting) | N/A |
| 22 | Define and version the metadata JSON contract | AC-6: A standard React window can be rendered from metadata |
| 23 | Implement the generic form renderer | AC-6 |
| 24 | Implement the generic grid | AC-6 |
| 25 | Implement list/table/search lookup controls | AC-7 |
| 26 | Implement search popup behavior | AC-7 |
| 27 | Implement display-logic evaluation | AC-8 |
| 28 | Implement field groups/layout metadata | AC-6 |
| 29 | Implement menu renderer | AC-6 |
| 30 | Provide a controlled custom-form escape hatch | AC-6 |

**HLD/LLD acceptance criteria for Phase 3:**

| # | Criterion | Test Scope |
|---|---|---|
| 6 | A standard React window can be rendered from metadata | Form, grid, menu, field groups |
| 7 | List/table/search references work generically | Lookup, search popup |
| 8 | Dynamic validation rules work with runtime context | Display logic, form validation |

## Test Summary

| Category | Count | Project / Location |
|---|---|---|
| 1. Unit Tests - Backend | 52 | Platform.Tests.Core (Runtime/DisplayLogic) |
| 2. Unit Tests - Frontend | 45 | frontend/src (*.test.{ts,tsx}) |
| 3. Integration Tests - API Endpoints | 30 | Platform.Tests.Integration (Runtime/Generic) |
| 4. Integration Tests - React Components (mocked API) | 20 | frontend/src (*.*.test.{ts,tsx}) |
| 5. E2E Tests - Full Flows | 12 | playwright/tests/e2e/ |
| 6. API Contract Tests | 25 | Platform.Tests.Integration (Contract/) |
| 7. Security Tests | 18 | Platform.Tests.Core (Security/) |
| 8. Performance Tests | 8 | Platform.Tests.Integration (Perf/) |
| 9. Regression Tests | 110 | All existing test projects |
| **Total** | **320** | |

**Note:** Regression tests are not new test cases; they are the existing 240 tests (Phase 0-2) that must continue to pass after Phase 3 changes. The "110" in this table represents the distinct test categories that must be verified, with many sub-scenarios. See Section 9 for details.

---

## 1. Unit Tests - Backend (52)

These tests verify the backend services that support the Phase 3 UI: the metadata contract builder, display-logic evaluator, and query-builder integration for generic grid filtering/sorting.

### 1.1 Window Metadata Builder (10 tests)

Builds the JSON contract shape from SysWindow + SysTab + SysField + SysFieldGroup + SysColumn + SysElement + SysReference + SysValRule.

| # | Test ID | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 1 | PU-WIN-001 | Builder assembles a window with one tab, multiple fields from metadata | SysWindow=1, SysTab=1, 5 SysFields, matching SysColumns | JSON object with correct windowId, name, one tab containing 5 fields | SysWindow 100 "Library Book" |
| 2 | PU-WIN-002 | Builder handles a window with no tabs | SysWindow exists, no SysTab rows | Tabs array is empty, not null | SysWindow 200, no tabs |
| 3 | PU-WIN-003 | Builder resolves SysElement label to field label | SysField.SysElementId=1, SysElement.Label="Title" | field.label = "Title" in JSON | Element label present |
| 4 | PU-WIN-004 | Builder resolves SysReference to controlType | SysField uses SysReference type TABLE | controlType = "TableLookup" in JSON | SysReferenceId pointing to TABLE ref |
| 5 | PU-WIN-005 | Builder resolves SysReference type LIST to ListDropdown | SysReference type LIST | controlType = "ListDropdown" in JSON | SysReference type = LIST |
| 6 | PU-WIN-006 | Builder resolves SysReference type SEARCH to SearchPopup | SysReference type SEARCH | controlType = "SearchPopup" in JSON | SysReference type = SEARCH |
| 7 | PU-WIN-007 | Builder sets isReadOnly from SysField properties | SysField.IsReadOnly=true | field.isReadOnly = true in JSON | SysField.IsReadOnly = true |
| 8 | PU-WIN-008 | Builder sets isMandatory from merged SysField+SysColumn rules | Both say mandatory | field.isMandatory = true | SysField.IsMandatory=true, SysColumn.IsMandatory=true |
| 9 | PU-WIN-009 | Builder sets displayLogic from SysField property | SysField.DisplayLogic="@>0" | field.displayLogic = "@>0" in JSON | displayLogic string in DB |
| 10 | PU-WIN-010 | Builder excludes inactive fields and inactive fields within inactive tabs | SysTab.IsActive=false, SysField.IsActive=false | Neither appears in output JSON | 2 inactive, 3 active |

### 1.2 Field Group Resolution (6 tests)

Field groups organize fields into sections within tabs.

| # | Test ID | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 11 | PU-FG-001 | Builder includes fieldGroup info on fields | SysFieldGroup defines group with seq | fields grouped under correct groupName | 2 groups, 8 fields |
| 12 | PU-FG-002 | Fields without a group are placed in default group | No SysFieldGroup row | Field appears in "default" group | 1 field, no group |
| 13 | PU-FG-003 | Inactive field groups exclude their fields from output | SysFieldGroup IsActive=false | Fields in that group not rendered | 1 inactive group |
| 14 | PU-FG-004 | Builder resolves fieldGroup label from SysElement | SysFieldGroup links to SysElement | Group label = element label | Element "Address" |
| 15 | PU-FG-005 | Nested field groups (if supported) maintain correct ordering | Multiple groups with SeqNo | Groups appear in seq order | SeqNo: 10, 20, 30 |
| 16 | PU-FG-006 | Same-line fields render as side-by-side layout hint | SysField.SameLine=true | Layout hint in JSON | sameLine = true |

### 1.3 Display Logic Evaluator (10 tests)

Evaluates display logic expressions to determine field visibility/readonly.

| # | Test ID | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 17 | PU-DL-001 | Evaluates simple boolean expression true | Expression="@CIsActive=true"; field is active | isVisible = true | "true" value |
| 18 | PU-DL-002 | Evaluates simple boolean expression false | Expression="@CIsActive=true"; field is inactive | isVisible = false | "false" value |
| 19 | PU-DL-003 | Evaluates AND condition | Expression="@A=1 AND @B=2"; A=1,B=2 | isVisible = true | A=1, B=2 |
| 20 | PU-DL-004 | Evaluates AND condition with one false | Expression="@A=1 AND @B=2"; A=1,B=3 | isVisible = false | A=1, B=3 |
| 21 | PU-DL-005 | Evaluates OR condition | Expression="@A=1 OR @B=2"; A=3,B=2 | isVisible = true | A=3, B=2 |
| 22 | PU-DL-006 | Evaluates NOT condition | Expression="NOT(@A=1)"; A=2 | isVisible = true | A=2 |
| 23 | PU-DL-007 | Evaluates comparison operators (> < >= <=) | Expression="@Qty>10"; Qty=15 | isVisible = true | Qty=15 |
| 24 | PU-DL-008 | Evaluates empty/null displayLogic as always-visible | No displayLogic or null | isVisible = true | null expression |
| 25 | PU-DL-009 | Rejects unsafe SQL in displayLogic | Expression="; DROP TABLE Users" | Throws SecurityException / returns error | SQL injection payload |
| 26 | PU-DL-010 | Resolves $TenantId, $UserId context variables | Expression="@Qty>$UserId"; UserId=5, Qty=10 | Uses resolved context; correct result | $UserId resolved to 5 |

### 1.4 Query Builder - Grid Filtering (10 tests)

Builds parameterized SQL for generic grid list endpoints with metadata-driven filters.

| # | Test ID | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 27 | PU-QB-001 | Build SELECT with no filters returns base query | Valid table metadata, no filter conditions | "SELECT ... FROM [table]" without WHERE | table="C_BPartner" |
| 28 | PU-QB-002 | Build SELECT with single-column filter | Filter: ColumnName="Name", Operator="like", Value="Acme" | WHERE "Name" LIKE @p0 with parameterized value | @p0 = "%Acme%" |
| 29 | PU-QB-003 | Build SELECT with multi-column AND filter | Two filter conditions, conjunction=AND | WHERE col1 = @p0 AND col2 = @p1 | col1="Name"=@p0, col2="City"=@p1 |
| 30 | PU-QB-004 | Build SELECT with multi-column OR filter | Two filter conditions, conjunction=OR | WHERE col1 = @p0 OR col2 = @p1 | col1="Name"=@p0, col2="City"=@p1 |
| 31 | PU-QB-005 | Rejects table name not in metadata allowlist | table="InjectedTable; DROP TABLE Users" | Throws ValidationException | SQL injection table name |
| 32 | PU-QB-006 | Rejects column name not in metadata allowlist | Filter references unknown column | Throws ValidationException | column="NonExistent" |
| 33 | PU-QB-007 | Applies tenant predicate automatically | TenantId=1 set in context | WHERE "TenantId" = @tenantId appended | tenantId=1 |
| 34 | PU-QB-008 | Applies org predicate when set | OrgId=5 set in context | WHERE "C_Org_ID" = @orgId appended | orgId=5 |
| 35 | PU-QB-009 | Generates ORDER BY from metadata OrderByClause | SysTable.OrderByClause="Name DESC" | ORDER BY "Name" DESC in SQL | Descending sort |
| 36 | PU-QB-010 | Generates paginated query with LIMIT/OFFSET | Page=2, PageSize=20 | OFFSET 20 LIMIT 20 in SQL | page=2, size=20 |

### 1.5 Query Builder - Grid Sorting (6 tests)

| # | Test ID | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 37 | PU-QS-001 | Sort by single column from metadata | SortBy="Name", SortDir="asc" | ORDER BY "Name" ASC | column allowed in metadata |
| 38 | PU-QS-002 | Sort by multiple columns | SortBy="Name,Date", SortDir="asc,desc" | ORDER BY "Name" ASC, "Date" DESC | Two columns |
| 39 | PU-QS-003 | Rejects sorting on non-projected column | SortBy="SomeOtherColumn" not in field list | Throws ValidationException | Column not in projection |
| 40 | PU-QS-004 | Rejects SQL injection in sort direction | SortDir="asc; DROP TABLE Users" | Throws ValidationException | Injection in direction |
| 41 | PU-QS-005 | Default sort applied when none specified | No sort metadata | Default sort from table metadata applied | Default: createdDate DESC |
| 42 | PU-QS-006 | Sort column names parameterized in metadata | SortBy is a real column | Column name resolved from metadata, not raw SQL | Column exists in SysColumn |

### 1.6 Metadata Contract Versioning (6 tests)

| # | Test ID | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 43 | PU-CT-001 | Response includes contract version header | API enabled | Header "X-Contract-Version: v1" returned | Version header |
| 44 | PU-CT-002 | Backward-compatible field addition does not break consumers | New optional field added | Existing consumers ignore new field; no error | New field "color" |
| 45 | PU-CT-003 | Breaking field rename returns 422 with migration guidance | Field "title" renamed to "name" | 422 status with "deprecated" notice | Deprecated field |
| 46 | PU-CT-004 | Contract version in response matches request version | Request accepts "v1" | Response shaped per v1 spec | v1 request |
| 47 | PU-CT-005 | Unknown contract version returns 415 | Request "v99" | 415 Unsupported Media Type | v99 |
| 48 | PU-CT-006 | Schema contract validation passes against JSON Schema | Full window response | Validation passes against schema file | Full window JSON |

### 1.7 Generic Data API - List Endpoint (4 tests)

| # | Test ID | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 49 | PU-DTA-001 | GET /api/data/{table} returns paginated list | Table with 50 records, page=1, size=10 | 10 records in data array; totalCount=50 | 50 rows in table |
| 50 | PU-DTA-002 | GET /api/data/{table} rejects unauthorized table | Table not in access control | 403 Forbidden | Unauthorized table |
| 51 | PU-DTA-003 | GET /api/data/{table} applies tenant predicate | Tenant=1 request | Only rows with TenantId=1 returned | 10 tenant-A rows, 10 tenant-B rows |
| 52 | PU-DTA-004 | GET /api/data/{table}/undefinedId returns 404 | Non-existent id | 404 Not Found | id=9999 |

### 1.8 Lookup Metadata Builder (6 tests)

Builds lookup definitions from SysReference + associated metadata.

| # | Test ID | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 53 | PU-LK-001 | Lookup metadata resolves reference type LIST | SysReference type LIST, 5 values | Array of 5 {key, value} pairs | 5 list values |
| 54 | PU-LK-002 | Lookup metadata resolves reference type TABLE | SysReference type TABLE, WhereClause defined | SQL template with correct columns | WhereClause="IsActive=1" |
| 55 | PU-LK-003 | Lookup metadata resolves reference type SEARCH | SysReference type SEARCH | SearchPopup definition with search columns | Search ref with 2 search columns |
| 56 | PU-LK-004 | Lookup metadata applies tenant filter | Reference on table with TenantId | Tenant predicate in SQL | tenantId=1 |
| 57 | PU-LK-005 | Lookup metadata handles empty reference set | LIST with 0 active values | Empty array returned | No list values |
| 58 | PU-LK-006 | Lookup metadata includes displayLogic from field | Field has displayLogic | displayLogic included in lookup response | displayLogic="@A=1" |

---

## 2. Unit Tests - Frontend (45)

These tests verify React components using Jest + @testing-library/react. They run via `react-scripts test` in the `frontend/` directory.

### 2.1 Window Renderer (8 tests)

| # | Test ID | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 59 | FU-WR-001 | Renders window name as heading | Window with name="Library Book" | H1 contains "Library Book" | window.name = "Library Book" |
| 60 | FU-WR-002 | Renders each tab as a tab panel | Window with 3 tabs | 3 Tab components rendered | 3 tabs |
| 61 | FU-WR-003 | Switching tabs changes active panel | Click tab 2 | Tab 2 panel visible; tab 1 hidden | 3 tabs |
| 62 | FU-WR-004 | Renders fields in order from metadata SeqNo | Fields with SeqNo 10, 5, 20 | Fields render in order 5, 10, 20 | Mixed seqNo |
| 63 | FU-WR-005 | Groups fields into field group sections | 2 field groups defined | Group headers rendered; fields nested | 2 groups |
| 64 | FU-WR-006 | Empty tabs array renders nothing | Window with no tabs | No tabs rendered, no error | Empty tabs |
| 65 | FU-WR-007 | Unknown controlType renders as TextInput with warning | controlType="CustomWidget" | TextInput rendered; console.warn called | Unknown type |
| 66 | FU-WR-008 | Loading state shows spinner before data arrives | React Query isLoading=true | Spinner/placeholder visible | Query not yet resolved |

### 2.2 Form Field Renderers (15 tests)

Each standard control type is tested for correct rendering.

| # | Test ID | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 67 | FU-FF-001 | TextInput renders with label and help text | controlType="TextInput" | Input element with correct placeholder; help text visible | Label="Title" |
| 68 | FU-FF-002 | NumberInput renders as number type with min/max | controlType="NumberInput", min=0, max=100 | input type=number; min/max attributes set | min=0, max=100 |
| 69 | FU-FF-003 | DateInput renders date picker | controlType="DateInput" | Input type=date rendered | N/A |
| 70 | FU-FF-004 | YesNoToggle renders as switch/checkbox | controlType="YesNoToggle" | Checkbox or toggle rendered | N/A |
| 71 | FU-FF-005 | ListDropdown renders options from reference values | controlType="ListDropdown", 3 values | Dropdown with 3 options | 3 list values |
| 72 | FU-FF-006 | TableLookup renders lookup button with value display | controlType="TableLookup" | Lookup button + selected value shown | Reference to table |
| 73 | FU-FF-007 | SearchPopup renders search input with results | controlType="SearchPopup" | Search input rendered | Reference type SEARCH |
| 74 | FU-FF-008 | TextArea renders as multiline | controlType="TextArea" | textarea element with correct rows | rows=3 |
| 75 | FU-FF-009 | ImageUpload renders file input | controlType="ImageUpload" | File input rendered | N/A |
| 76 | FU-FF-010 | Mandatory field shows asterisk indicator | isMandatory=true | Visual asterisk on label | Required field |
| 77 | FU-FF-011 | Read-only field is disabled | isReadOnly=true | Input disabled; value shown but not editable | ReadOnly=true |
| 78 | FU-FF-012 | Field with defaultValue shows pre-populated value | defaultValue="Default Value" | Input shows "Default Value" | Default set |
| 79 | FU-FF-013 | Field errors display validation messages | Validation error on field | Error message below input, red styling | Error text |
| 80 | FU-FF-014 | Display logic false hides field | displayLogic="@A=1", A=false | Field not rendered in DOM | Hidden field |
| 81 | FU-FF-015 | Display logic true shows field | displayLogic="@A=1", A=true | Field rendered in DOM | Visible field |

### 2.3 react-hook-form Integration (7 tests)

| # | Test ID | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 82 | FU-FM-001 | Form registers fields with react-hook-form | Window with 5 fields | All 5 fields registered | 5 fields |
| 83 | FU-FM-002 | Form submit collects values from all fields | User fills all fields, submits | handleSubmit receives all values | Valid data |
| 84 | FU-FM-003 | Mandatory validation rejects empty required field | isMandatory=true, value empty | Form submission prevented; error shown | Empty mandatory |
| 85 | FU-FM-004 | Form reset clears all values | Form with values filled | reset() clears all inputs | Filled form |
| 86 | FU-FM-005 | Form handles nested object values correctly | Parent/detail form | Nested values handled by RHF | Nested data |
| 87 | FU-FM-006 | Form prevents double-submit | User double-clicks submit | Only one API call made | Double click |
| 88 | FU-FM-007 | Form shows loading state during API call | onSubmit triggers async call | Submit button disabled; spinner shown | API pending |

### 2.4 Grid Renderer (7 tests)

| # | Test ID | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 89 | FU-GR-001 | Grid renders column headers from metadata | 5 columns in metadata | 5 column headers in table | 5 columns |
| 90 | FU-GR-002 | Grid renders row data | 20 records returned | 20 rows in table | 20 records |
| 91 | FU-GR-003 | Grid shows loading state | Query isLoading=true | Loading indicator visible | Query pending |
| 92 | FU-GR-004 | Grid shows error state | Query error | Error message rendered | API error |
| 93 | FU-GR-005 | Grid empty state shows no-data message | Zero records | "No data" message | Empty result |
| 94 | FU-GR-006 | Grid pagination UI controls visible | 50 records, pageSize=10 | Page numbers/next button | 5 pages |
| 95 | FU-GR-007 | Clicking row selects it | Row clicked | Row highlighted; onSelect callback invoked | Click row 3 |

### 2.5 Lookup/Search Components (5 tests)

| # | Test ID | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 96 | FU-LK-001 | TableLookup opens popup with results | Reference type TABLE | Popup with table data | 10 results |
| 97 | FU-LK-002 | SearchPopup filters results by search text | Search text "Acme" | Results filtered to "Acme" | Text="Acme" |
| 98 | FU-LK-003 | Selecting a lookup value fills form field | Row selected in popup | Value populated in field; popup closes | Selected row |
| 99 | FU-LK-004 | ListDropdown shows selected value | Value already set | Dropdown shows correct selection | Selected value |
| 100 | FU-LK-005 | Lookup pagination loads more results | 200 total, page=1 | Page 1 shows first batch | 200 results |

---

## 3. Integration Tests - API Endpoints (30)

These tests hit the real ASP.NET Core API against a PostgreSQL container. They test the full backend stack for generic CRUD, lookup, and metadata endpoints.

### 3.1 Window Metadata Endpoint (6 tests)

| # | Test ID | Category | Description | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 101 | PI-WIN-001 | GET /api/meta/window/1 returns valid contract | Window 1 exists with tabs and fields | 200 OK; JSON matches contract schema | Window 100 with 2 tabs, 8 fields |
| 102 | PI-WIN-002 | GET /api/meta/window/999 returns 404 | Window 999 does not exist | 404 Not Found | Non-existent window |
| 103 | PI-WIN-003 | GET /api/meta/window/1 returns contract version header | Window exists | Header X-Contract-Version present | Window 100 |
| 104 | PI-WIN-004 | Window with inactive tab excludes tab from response | Tab IsActive=false | Tab not in tabs array | 1 active, 1 inactive tab |
| 105 | PI-WIN-005 | Window with inactive field excludes field from response | Field IsActive=false | Field not in fields array | 1 active, 1 inactive field |
| 106 | PI-WIN-006 | Window metadata includes validation rules | Field has SysValRuleId | valRule fields populated in response | ValRule attached |

### 3.2 Generic CRUD API (12 tests)

| # | Test ID | Category | Description | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 107 | PI-CRD-001 | POST /api/data/{table} creates record | Valid payload for registered table | 201 Created; record in DB with ID | Valid payload |
| 108 | PI-CRD-002 | POST /api/data/{table} with mandatory field missing | Missing mandatory field | 422 Unprocessable Entity with validation errors | Missing required |
| 109 | PI-CRD-003 | POST /api/data/{table} with type mismatch | Integer field receives string | 422 with type validation error | Wrong type |
| 110 | PI-CRD-004 | PUT /api/data/{table}/{id} updates record | Valid update payload | 200 OK; record updated in DB | Updated values |
| 111 | PI-CRD-005 | PUT /api/data/{table}/{id} with non-existent id | id=9999 | 404 Not Found | Non-existent |
| 112 | PI-CRD-006 | DELETE /api/data/{table}/{id} deletes record | Valid delete | 204 No Content; record removed | Existing record |
| 113 | PI-CRD-007 | GET /api/data/{table} returns paginated results | Table with 50 records, pageSize=10 | 10 records in response; pagination metadata | 50 records |
| 114 | PI-CRD-008 | GET /api/data/{table} filters by single column | Filter: Name like "Acme" | Only matching records returned | 5 of 50 match |
| 115 | PI-CRD-009 | GET /api/data/{table} sorts by specified column | SortBy=Name, SortDir=asc | Results sorted ascending | Sorted results |
| 116 | PI-CRD-010 | GET /api/data/{table} rejects unknown sort column | SortBy=InjectedColumn | 400 Bad Request | Unknown column |
| 117 | PI-CRD-011 | POST with ValRule violation rejected | ValRule on field, invalid value | 422 with ValRule error message | Invalid value |
| 118 | PI-CRD-012 | GET /api/data/{table} applies tenant predicate | Tenant=1, data for tenant 1 and 2 | Only tenant-1 records returned | Mixed tenant data |

### 3.3 Lookup API (6 tests)

| # | Test ID | Category | Description | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 119 | PI-LKP-001 | GET /api/lookup/{refId} returns list values | Reference type LIST, 5 values | 5 key-value pairs in response | 5 list values |
| 120 | PI-LKP-002 | GET /api/lookup/{refId} with search text | Reference type SEARCH, search="Acme" | Results filtered to "Acme" | 3 of 50 match |
| 121 | PI-LKP-003 | GET /api/lookup/{refId} pagination | Reference type TABLE, pageSize=20 | 20 results in response | 200 total results |
| 122 | PI-LKP-004 | GET /api/lookup/{refId} tenant filter | Tenant=1 | Only tenant-1 records | Mixed tenant |
| 123 | PI-LKP-005 | GET /api/lookup/999 returns 404 | Non-existent reference | 404 Not Found | Non-existent |
| 124 | PI-LKP-006 | GET /api/lookup/{refId} applies table reference WHERE clause | WhereClause="IsActive=1" | Only active records returned | 5 of 20 active |

### 3.4 Field Group Metadata (3 tests)

| # | Test ID | Category | Description | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 125 | PI-FG-001 | Window metadata includes field group structure | Field groups defined | Groups with fields in response | 2 groups, 8 fields |
| 126 | PI-FG-002 | Field group with no fields excluded from response | Empty field group | Group not in output | 0 fields in group |
| 127 | PI-FG-003 | Field group ordering respects SeqNo | Groups with mixed SeqNo | Groups in correct order | SeqNo: 20, 10, 30 |

### 3.5 Menu Metadata Endpoint (3 tests)

| # | Test ID | Category | Description | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 128 | PI-MNU-001 | GET /api/meta/menu returns menu tree | Menu items with parent-child | Hierarchical tree structure | 5 items, 2 levels |
| 129 | PI-MNU-002 | GET /api/meta/menu filters by user role | User has role "Manager" | Only authorized menu items | Role-based items |
| 130 | PI-MNU-003 | GET /api/meta/menu returns empty for unauthorized user | User has no menu access | Empty array | No access |

### 3.6 Escape Hatch - Custom Form Endpoint (3 tests)

| # | Test ID | Category | Description | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 131 | PI-ESC-001 | GET /api/meta/window/{id} with customForm=true returns custom metadata | Custom form registered | Custom form metadata returned | Custom form |
| 132 | PI-ESC-002 | Custom form rendering falls through to generic renderer for standard fields | Mix of custom + standard fields | Standard fields rendered generically | Mixed fields |
| 133 | PI-ESC-003 | Unauthorized custom form access rejected | User not authorized | 403 Forbidden | Unauthorized |

---

## 4. Integration Tests - React Components with Mocked API (20)

These tests render React components with mocked API responses via axios-mock-adapter (or MSW). They verify component behavior without hitting the real API but with real data shapes.

### 4.1 Window with Form (8 tests)

| # | Test ID | Description | Mock Setup | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 134 | CI-FORM-001 | Form renders and submits to API | Mock GET /api/meta/window/1, POST /api/data/test | Form fills, submits, API called once | Window with 3 fields |
| 135 | CI-FORM-002 | Form validation prevents submission with invalid data | Mock GET endpoint | Submit fails; errors displayed | Missing mandatory |
| 136 | CI-FORM-003 | Form loads existing data for edit | Mock GET /api/data/test/1 | Values pre-populated | Existing record |
| 137 | CI-FORM-004 | Form handles API error on submit | Mock POST returns 500 | Error toast/banner shown | 500 error |
| 138 | CI-FORM-005 | Form handles network failure | Mock POST rejects connection | Error state with retry option | Network error |
| 139 | CI-FORM-006 | Form with field groups renders group dividers | Mock GET with groups | Group sections rendered | 2 groups |
| 140 | CI-FORM-007 | Form with display logic hides fields dynamically | Mock GET with displayLogic | Fields conditionally rendered | displayLogic="@A=1" |
| 141 | CI-FORM-008 | Form escape hatch renders custom component | Mock returns customForm=true | Custom component rendered | Custom form flag |

### 4.2 Grid with Data (7 tests)

| # | Test ID | Description | Mock Setup | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 142 | CI-GRID-001 | Grid renders from mocked data | Mock GET /api/data/test | Table with rows rendered | 10 records |
| 143 | CI-GRID-002 | Grid pagination works | Mock returns paginated data | Page navigation works | 50 records, 10 per page |
| 144 | CI-GRID-003 | Grid column sorting works | Client-side sort on mock data | Columns sortable | 10 records |
| 145 | CI-GRID-004 | Grid filter input works | Mock GET with filter param | Filtered results shown | Filter text |
| 146 | CI-GRID-005 | Grid loading state during data fetch | Mock with delay | Loading spinner shown | Pending request |
| 147 | CI-GRID-006 | Grid error state on API failure | Mock returns 500 | Error banner shown | 500 error |
| 148 | CI-GRID-007 | Grid empty state when no data | Mock returns empty array | Empty state message | 0 records |

### 4.3 Lookup Components (5 tests)

| # | Test ID | Description | Mock Setup | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 149 | CI-LKP-001 | Lookup dropdown shows reference values | Mock GET /api/lookup/1 | Dropdown populated | 5 values |
| 150 | CI-LKP-002 | Search popup filters on input | Mock GET /api/lookup/1 with search | Results filtered | Search text |
| 151 | CI-LKP-003 | Selected lookup value fills form field | Value selected in lookup | Field populated | Selected value |
| 152 | CI-LKP-004 | Lookup pagination loads more | Mock paginated results | More results load on page change | 200 results |
| 153 | CI-LKP-005 | Lookup API error shows error state | Mock returns 500 | Error state in lookup | 500 error |

---

## 5. E2E Tests - Full Flows (12)

These tests use Playwright to test the full stack in the browser. They cover the complete user journey: register table -> render form -> create -> read -> update -> delete.

**Prerequisites:** Full platform running (API + Frontend + PostgreSQL). Playwright test runner configured with `playwright.config.ts`.

### 5.1 Standard CRUD Lifecycle (5 tests)

| # | Test ID | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 154 | EE-CRD-001 | Create record via generic form | Window registered for test table; user logged in | Record created; success message; record visible in grid | Valid form data |
| 155 | EE-CRD-002 | Read record from grid -> detail view | Record exists in grid | Record opened in form view with correct values | Existing record |
| 156 | EE-CRD-003 | Update record via generic form | Record in edit mode | Changes saved; grid reflects update | Updated values |
| 157 | EE-CRD-004 | Delete record via grid | Record selected in grid | Record deleted; confirmation shown; row removed | Existing record |
| 158 | EE-CRD-005 | Full CRUD lifecycle in one flow | Clean table | Create -> List -> Update -> Delete all succeed | Full lifecycle |

### 5.2 Validation Enforcement (3 tests)

| # | Test ID | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 159 | EE-VAL-001 | Mandatory field prevents form submission | Field is mandatory | Submit blocked; error shown | Empty mandatory |
| 160 | EE-VAL-002 | Type validation blocks invalid input | Field type=INTEGER; user enters text | Error message; submit blocked | "abc" for integer |
| 161 | EE-VAL-003 | ValRule validation blocks invalid pattern | ValRule regex on field | Error message; submit blocked | Invalid regex match |

### 5.3 Lookup in Form (2 tests)

| # | Test ID | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 162 | EE-LKP-001 | TableLookup in form selects value | Reference table with data | Lookup popup opens; value selected; saved with record | 10 reference records |
| 163 | EE-LKP-002 | SearchPopup filters reference data | Reference type SEARCH with data | Search text filters results; valid selection | 50 reference records |

### 5.4 Display Logic (2 tests)

| # | Test ID | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 164 | EE-DL-001 | Field with displayLogic hidden when condition false | displayLogic="@Type=1"; Type field set to "2" | Conditional field not visible | Type != expected |
| 165 | EE-DL-002 | Field with displayLogic shown when condition true | displayLogic="@Type=1"; Type field set to "1" | Conditional field visible | Type = expected |

### 5.5 Menu Navigation (2 tests)

| # | Test ID | Description | Prerequisites | Expected Outcome | Test Data |
|---|---|---|---|---|---|
| 166 | EE-MNU-001 | Click menu item navigates to window | Menu item linked to window | Window renders correctly | Menu -> window link |
| 167 | EE-MNU-002 | Unauthorized menu items hidden from menu | User without access | Menu item not visible | Unauthorized |

---

## 6. API Contract Tests (25)

These tests verify that API responses conform to the expected JSON schema, status codes, and header conventions. They can be implemented as a separate test project (Platform.Tests.Contract) or as a sub-category of integration tests.

### 6.1 Metadata API Contract (10 tests)

| # | Test ID | Description | Expected Outcome | Test Data |
|---|---|---|---|---|
| 168 | PC-MET-001 | GET /api/meta/window/{id} returns 200 with correct shape | Schema validation passes | Existing window |
| 169 | PC-MET-002 | GET /api/meta/window/{id} returns 404 for missing | Status 404 | Non-existent |
| 170 | PC-MET-003 | Response includes X-Contract-Version header | Header present and valid version | Existing window |
| 171 | PC-MET-004 | Response Content-Type is application/json | Content-Type header correct | Any request |
| 172 | PC-MET-005 | tabs array is array of objects with table, fields | tabs[i] has table string, fields array | Any window |
| 173 | PC-MET-006 | fields array contains required properties | Each field has columnName, label, controlType | Any window |
| 174 | PC-MET-007 | displayLogic is null or string, never object | Type of displayLogic is string|null | Any window |
| 175 | PC-MET-008 | controlType value is from allowed enum | controlType in [TextInput, NumberInput, etc.] | Any window |
| 176 | PC-MET-009 | isMandatory and isReadOnly are boolean, never string | Correct types | Any window |
| 177 | PC-MET-010 | Response body is never empty for existing window | Response has body with content | Existing window |

### 6.2 Data API Contract (8 tests)

| # | Test ID | Description | Expected Outcome | Test Data |
|---|---|---|---|---|
| 178 | PC-DTA-001 | GET /api/data/{table} returns {data: [], pagination: {}} | Response shape correct | Existing table |
| 179 | PC-DTA-002 | Pagination object includes totalCount, page, pageSize | All 3 fields present | Any table |
| 180 | PC-DTA-003 | GET /api/data/{table}/{id} returns single object | Single object, not array | Existing record |
| 181 | PC-DTA-004 | POST /api/data/{table} returns 201 with location header | Status 201; Location header | Valid payload |
| 182 | PC-DTA-005 | POST with validation errors returns 422 with errors array | Status 422; errors[].field, errors[].message | Invalid payload |
| 183 | PC-DTA-006 | PUT /api/data/{table}/{id} returns 200 with updated object | Status 200; object returned | Valid update |
| 184 | PC-DTA-007 | DELETE /api/data/{table}/{id} returns 204 with no body | Status 204; empty body | Existing record |
| 185 | PC-DTA-008 | GET /api/data/unknown-table returns 400 | Status 400 | Unknown table |

### 6.3 Lookup API Contract (7 tests)

| # | Test ID | Description | Expected Outcome | Test Data |
|---|---|---|---|---|
| 186 | PC-LKP-001 | GET /api/lookup/{id} returns array of {key, value} | Correct item shape | Existing reference |
| 187 | PC-LKP-002 | GET /api/lookup/{id} with search param filters | Results contain search text | Search text |
| 188 | PC-LKP-003 | GET /api/lookup/{id} pagination headers | Pagination metadata in response | Paginated results |
| 189 | PC-LKP-004 | GET /api/lookup/unknown returns 404 | Status 404 | Non-existent ref |
| 190 | PC-LKP-005 | LIST reference returns key-value pairs | Each item has key (int) and value (string) | LIST ref |
| 191 | PC-LKP-006 | TABLE reference returns key-value with display columns | Each item has key + display fields | TABLE ref |
| 192 | PC-LKP-007 | SEARCH reference supports wildcard search | Search results contain partial matches | Partial search text |

---

## 7. Security Tests (18)

These tests verify that Phase 3 UI features do not introduce security regressions and that server-side enforcement holds.

### 7.1 Authorization (6 tests)

| # | Test ID | Description | Expected Outcome | Test Data |
|---|---|---|---|---|
| 193 | PS-AUTH-001 | Unauthenticated request to window metadata returns 401 | No auth token | Any window |
| 194 | PS-AUTH-002 | Authenticated user without window access gets 403 | Auth token present, no window access | User without access |
| 195 | PS-AUTH-003 | User with only READ access cannot POST | User has read-only role | POST request |
| 196 | PS-AUTH-004 | User with only READ access cannot DELETE | User has read-only role | DELETE request |
| 197 | PS-AUTH-005 | Token expiration returns 401 on API call | Expired JWT token | 401 |
| 198 | PS-AUTH-006 | Valid token with correct role allows access | Token with valid role | 200 OK |

### 7.2 Tenant Isolation (6 tests)

| # | Test ID | Description | Expected Outcome | Test Data |
|---|---|---|---|---|
| 199 | PS-TEN-001 | Tenant A cannot read Tenant B records via grid | Tenant A user, Tenant B data | 0 Tenant B records |
| 200 | PS-TEN-002 | Tenant A cannot update Tenant B records via PUT | Tenant A user, Tenant B record ID | 403 Forbidden |
| 201 | PS-TEN-003 | Tenant A cannot delete Tenant B records via DELETE | Tenant A user, Tenant B record ID | 403 Forbidden |
| 202 | PS-TEN-004 | Tenant predicate applied in grid query parameterized | Tenant A user, SQL trace | SQL includes WHERE "TenantId"=@p0 |
| 203 | PS-TEN-005 | Lookup API applies tenant filter | Tenant A user, lookup on multi-tenant table | Only Tenant A results |
| 204 | PS-TEN-006 | Tenant ID cannot be spoofed via form payload | Payload includes TenantId=B | Server ignores payload tenant; uses token tenant |

### 7.3 XSS Prevention (3 tests)

| # | Test ID | Description | Expected Outcome | Test Data |
|---|---|---|---|---|
| 205 | PS-XSS-001 | HTML in field label not rendered as HTML | Label="<script>alert(1)</script>" | Label rendered as text, not HTML | Script in label |
| 206 | PS-XSS-002 | HTML in field value not rendered as HTML | Value="<img onerror=alert(1)>" | Value rendered as text in DOM | Script in value |
| 207 | PS-XSS-003 | DisplayLogic expression cannot inject script | displayLogic="@x=<script>" | Expression rejected or treated as literal | Script in logic |

### 7.4 SQL Injection (3 tests)

| # | Test ID | Description | Expected Outcome | Test Data |
|---|---|---|---|---|
| 208 | PS-SQL-001 | Table name parameterized, not concatenated | Table param="Users; DROP TABLE Users" | Query fails; table not found in metadata |
| 209 | PS-SQL-002 | Filter value parameterized in WHERE | Filter value="'; DROP TABLE Users; --" | Parameterized query; no table dropped |
| 210 | PS-SQL-003 | WhereClause from metadata validated against allowlist | WhereClause with SQL injection | Rejected during build; exception thrown |

### 7.5 Custom Escape Hatch Security (3 tests)

| # | Test ID | Description | Expected Outcome | Test Data |
|---|---|---|---|---|
| 211 | PS-ESC-001 | Custom form requires explicit authorization | User without custom form access | 403 Forbidden |
| 212 | PS-ESC-002 | Custom form cannot bypass field-level security | Custom form + restricted field | Field not rendered or read-only |
| 213 | PS-ESC-003 | Custom form submit still runs validation pipeline | Custom form with invalid data | Validation errors returned |

---

## 8. Performance Tests (8)

These tests validate that Phase 3 components perform acceptably under load. They can be implemented as integration tests with timing assertions or as separate load tests.

### 8.1 Grid Performance (5 tests)

| # | Test ID | Description | Expected Outcome | Test Data |
|---|---|---|---|---|
| 214 | PF-GRD-001 | Grid renders 1000 rows in < 2 seconds | 1000 records, virtualized rendering | Render time < 2s |
| 215 | PF-GRD-002 | Grid pagination with 100k rows loads page in < 500ms | 100k records, pageSize=20 | Page load < 500ms |
| 216 | PF-GRD-003 | Sorting 10k rows client-side < 1 second | 10k records, client sort | Sort < 1s |
| 217 | PF-GRD-004 | Filtering 50k rows server-side < 1 second | 50k records, filter on indexed column | Filter < 1s |
| 218 | PF-GRD-005 | Concurrent grid loads do not degrade each other > 20% | 5 grids loading simultaneously | All < 1.2x single load time |

### 8.2 API Performance (3 tests)

| # | Test ID | Description | Expected Outcome | Test Data |
|---|---|---|---|---|
| 219 | PF-API-001 | GET /api/meta/window/{id} returns in < 200ms | Window with full metadata | P95 < 200ms |
| 220 | PF-API-002 | GET /api/data/{table} paginated returns in < 500ms | 10k rows, pageSize=20 | P95 < 500ms |
| 221 | PF-API-003 | GET /api/lookup/{id} returns in < 100ms | LIST reference | P95 < 100ms |

---

## 9. Regression Tests (existing 240 tests must still pass)

This section enumerates what must be verified after Phase 3 implementation. These are not new tests -- they are the existing Phase 0-2 tests whose results must remain PASS.

### 9.1 Phase 1 Tests (82 tests)

| Category | Count | Must Still Pass |
|---|---|---|
| Dictionary model tests (SysColumn, SysElement, SysReference, SysReferenceList, SysReferenceTable, SysTranslation, SysTable, SysValRule) | 22 | All assertions unchanged |
| Schema contract tests (8 tables, all columns, constraints, seed data, FK integrity) | 33 | All assertions unchanged |
| Dictionary migration integration tests | 27 | All assertions unchanged |

### 9.2 Phase 2 Tests (158 tests)

| Category | Count | Must Still Pass |
|---|---|---|
| Metadata Graph tests (construction, traversal, circular detection) | 5 | Graph unchanged |
| Metadata loading tests | 4 | Repository contracts unchanged |
| IMemoryCache tests (TTL, concurrency, null handling) | 8 | Cache layer unchanged |
| Redis distributed cache tests | 5 | Redis layer unchanged |
| Cache miss behavior tests | 3 | Cache behavior unchanged |
| Cache refresh / graph invalidation tests | 4 | Invalidation unchanged |
| Type validation tests | 7 | Validators unchanged |
| Reference validation tests | 4 | Validators unchanged |
| ValRule evaluation tests | 5 | Engine unchanged |
| Context variable resolver tests | 3 | Resolver unchanged |
| PO validation pipeline tests | 4 | Pipeline unchanged |
| PO lifecycle hook tests | 4 | Hooks unchanged |
| PO factory tests | 3 | Factory unchanged |
| Cache invalidation integration tests | 6 | Integration behavior unchanged |
| ValRule real DB integration tests | 4 | DB execution unchanged |
| Redis reconnect / resubscribe tests | 3 | Redis lifecycle unchanged |
| Distributed cache E2E integration tests | 3 | Pub/sub unchanged |
| Security edge case tests (whitelist, tenant isolation, table allowlist) | 18 | Security unchanged |
| PO Factory integration tests | 4 | DB integration unchanged |
| POLifecycle integration tests | 6 | DB integration unchanged |

### 9.3 Regression Verification Method

After Phase 3 implementation, run:

```
# All existing tests must pass
dotnet test tests/Platform.Tests.Core/Platform.Tests.Core.csproj
dotnet test tests/Platform.Tests.SchemaContract/Platform.Tests.SchemaContract.csproj
dotnet test tests/Platform.Tests.Integration/Platform.Tests.Integration.csproj

# CI must pass
# GitHub Actions CI pipeline verification
```

**No regression test count is summed as a separate total.** The regression requirement is: 100% of the 240 existing tests must pass with identical results. Any failure indicates a regression that must be fixed, not a test to be weakened.

---

## Test File Organization

```
tests/
  Platform.Tests.Core/
    Runtime/
      DisplayLogicEvaluatorTests.cs          # 1.3 - Tests 17-26 (10 tests)
      WindowMetadataBuilderTests.cs          # 1.1, 1.2, 1.8 - Tests 1-16, 53-58 (22 tests)
      QueryBuilderTests.cs                   # 1.4, 1.5 - Tests 27-42 (16 tests)
      MetadataContractTests.cs               # 1.6 - Tests 43-48 (6 tests)
    Security/
      AuthorizationTests.cs                  # 7.1 - Tests 193-198 (6 tests)
      TenantIsolationTests.cs                # 7.2 - Tests 199-204 (6 tests)
      EscapeHatchSecurityTests.cs            # 7.5 - Tests 211-213 (3 tests)

  Platform.Tests.Integration/
    Runtime/
      GenericCrudTests.cs                    # 3.2 - Tests 107-118 (12 tests)
      LookupApiTests.cs                      # 3.3 - Tests 119-124 (6 tests)
      FieldGroupTests.cs                     # 3.4 - Tests 125-127 (3 tests)
      WindowMetadataApiTests.cs              # 3.1 - Tests 101-106 (6 tests)
    Contract/
      MetadataContractTests.cs               # 6.1 - Tests 168-177 (10 tests)
      DataApiContractTests.cs                # 6.2 - Tests 178-185 (8 tests)
      LookupApiContractTests.cs              # 6.3 - Tests 186-192 (7 tests)
    Perf/
      GridPerformanceTests.cs                # 8.1 - Tests 214-218 (5 tests)
      ApiPerformanceTests.cs                 # 8.2 - Tests 219-221 (3 tests)
    MenuTests.cs                             # 3.5 - Tests 128-130 (3 tests)
    EscapeHatchTests.cs                      # 3.6 - Tests 131-133 (3 tests)

frontend/
  src/
    components/
      WindowRenderer.test.tsx                # 2.1 - Tests 59-66 (8 tests)
      FormFieldRenderers.test.tsx            # 2.2 - Tests 67-81 (15 tests)
      FormSubmission.test.tsx                # 2.3 - Tests 82-88 (7 tests)
      GridRenderer.test.tsx                  # 2.4 - Tests 89-95 (7 tests)
      LookupComponents.test.tsx              # 2.5 - Tests 96-100 (5 tests)
    integration/
      FormWithApi.test.tsx                   # 4.1 - Tests 134-141 (8 tests)
      GridWithApi.test.tsx                   # 4.2 - Tests 142-148 (7 tests)
      LookupWithApi.test.tsx                 # 4.3 - Tests 149-153 (5 tests)

playwright/
  tests/
    e2e/
      crud-lifecycle.spec.ts                 # 5.1 - Tests 154-158 (5 tests)
      validation.spec.ts                     # 5.2 - Tests 159-161 (3 tests)
      lookup-in-form.spec.ts                 # 5.3 - Tests 162-163 (2 tests)
      display-logic.spec.ts                  # 5.4 - Tests 164-165 (2 tests)
      menu-navigation.spec.ts                # 5.5 - Tests 166-167 (2 tests)
```

---

## CI Additions for Phase 3

The CI pipeline (`.github/workflows/ci.yml`) must be extended to:

1. **Run frontend unit tests** after frontend build:
   ```
   cd frontend && npm test -- --watchAll=false
   ```

2. **Run frontend integration tests** (components with mocked API):
   ```
   cd frontend && npm test -- --watchAll=false --testPathPattern=integration
   ```

3. **Run E2E tests** after API + frontend are both running:
   ```
   npx playwright test --project=chromium
   ```
   Requires a running API and frontend instance. Use a separate CI job with `service` containers.

4. **Run performance tests** as a gated step (not on every PR, only on merge to main):
   ```
   dotnet test --filter "Category=perf"
   ```

5. **Add Playwright browser** to CI:
   ```
   npx playwright install --with-deps chromium
   ```

6. **Add API contract schema validation** step:
   Generate JSON Schema from the React metadata contract and validate API responses against it in CI.

---

## Test ID Convention

```
{CATEGORY}-{SUBJECT}-{NUMBER}

Categories:
  PU - Unit (Backend, pure logic)
  FU - Unit (Frontend, React components)
  PI - Integration (Backend, API endpoints)
  CI - Integration (Frontend, mocked API)
  EE - E2E (Browser, full stack)
  PC - API Contract (Schema validation)
  PS - Security (Auth, isolation, injection)
  PF - Performance (Load, latency)
```

---

## Risk Register

| # | Risk | Impact | Mitigation |
|---|---|---|---|
| 1 | Frontend test suite requires new dependencies (axios-mock-adapter, @testing-library/user-event v21) | Build complexity, install time | Add dependencies to frontend/package.json; document in setup |
| 2 | Playwright E2E requires browser installation in CI | CI time increases ~3 minutes | Install once per job, cache browser binaries |
| 3 | Performance tests require large seed data | Test setup complexity | Create seed procedure; reuse across perf tests |
| 4 | Display logic evaluator grammar may evolve | Tests brittle to expression format | Test against evaluator API, not raw expression strings |
| 5 | JSON contract versioning drift between backend and frontend | Mismatched expectations | Contract tests (Section 6) catch this early |
| 6 | Mocked API integration tests may not catch server-side bugs | False positive confidence | Ensure all server-side paths are covered by backend integration tests (Section 3) |
| 7 | Cross-tenant E2E tests require multi-user session management | E2E test complexity | Use separate API tokens per tenant in E2E tests |
| 8 | Grid virtualization may affect test rendering | @testing-library queries return different element counts | Use testing-library selectors that are robust to virtualization |

---

## Sign-off Criteria

This phase passes its gate when:

1. All 81 new tests (excluding regression) compile and run locally.
2. All 240 existing tests (Phase 0-2) still pass (regression).
3. CI passes with all new tests on GitHub Actions (PostgreSQL + Redis services).
4. Frontend `npm test` passes with 0 failures.
5. E2E tests pass on Chromium in CI.
6. API contract tests validate against JSON Schema in CI.
7. Security tests verify auth, tenant isolation, XSS, and SQL injection.
8. Performance tests show acceptable P95 latencies.
9. Phase gate script (`scripts/phase-gate.ps1`) returns PASS for Phase 3.

---

## Design Closure Update (2026-08-15)

### Test Count Update

The preflight planned 210 new tests + 240 regression = 450 total.

This design closure confirms:
- **210 new tests** across 8 categories (unchanged — the number was already correct)
- **240 regression tests** from Phases 0-2 (unchanged)
- **Grand total: 450 tests**

### Security Finding Coverage

All 12 Critical/High findings have test coverage:
- SEC-P3-003 (SQL injection): ST-001 through ST-004, BU-034 through BU-036, BU-027
- SEC-P3-004 (XSS): ST-005 through ST-008
- SEC-P3-005 (Overbroad projection): ST-016
- SEC-P3-006 (Column access): ST-014, ST-015
- SEC-P3-010 (Display logic bypass): ST-009, ST-010, BU-024, BU-027
- SEC-P3-007 (DoS on lookups): ST-017, ST-018

Critical findings SEC-P3-001 (auth) and SEC-P3-002 (tenant isolation) are Phase 4 tests.

### Requirements Traceability

Every HLD/LLD Item 21-30 has at least one corresponding test:
- Item 21 (Standardize React): FU unit tests (linting, types)
- Item 22 (Metadata contract): CT-001 through CT-025 (25 contract tests)
- Item 23 (Generic form): BU-001-012, FU-009-025, AI-013-016, RI-001-007, E2E-001-005
- Item 24 (Generic grid): FU-026-035, AI-007-009, RI-008-011, E2E-009-010
- Item 25 (Lookup): BU-046-049, FU-036-039, AI-019-024, E2E-006
- Item 26 (Search popup): BU-048-049, FU-040, RI-012-013, E2E-006
- Item 27 (Display logic): BU-019-028, FU-004-005, RI-004-005, E2E-007, ST-009-011
- Item 28 (Field groups): BU-013-018, AI-025-027, RI-015
- Item 29 (Menu): BU-050-052, FU-041-045, AI-028-030, E2E-008, PT-007
- Item 30 (Custom form): AI-031-033, RI-018
