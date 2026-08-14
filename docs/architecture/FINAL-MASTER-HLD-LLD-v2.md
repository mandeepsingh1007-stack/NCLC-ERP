# No-Code / Low-Code Platform Framework
# FINAL MASTER HLD + LLD v2
## Developer Implementation Specification

**Status:** Final implementation baseline  
**Audience:** Backend, Frontend, Database, Platform, DevOps, QA  
**Backend:** .NET 8 / ASP.NET Core  
**Database:** PostgreSQL 15+  
**Frontend:** React + TypeScript  
**Data Access:** Dapper + Npgsql  

---

# 1. Executive Summary

This document defines the target architecture and detailed implementation design for a metadata-driven No-Code / Low-Code application platform.

The platform is dictionary-first: tables, columns, elements, references, validation rules, windows, tabs, fields, menus, processes, workflows, security and other platform behavior are represented as metadata and consumed by generic runtime services.

The implementation must provide:

- Generic metadata-driven CRUD.
- Strongly typed generated `X_<Table>` classes.
- Hand-written `M_<Table>` business classes.
- Metadata-driven React forms and grids.
- List, table and search references.
- Dynamic validation rules.
- Multi-client / organization / role security.
- Window, process, table, column and record access.
- Process execution and workflow runtime.
- Audit/change history.
- Generic attachments, trees and sequences.
- Versioned installable modules.
- Upgrade-safe dictionary ownership.
- Distributed metadata-cache invalidation.
- A controlled escape hatch for custom screens.

The architecture is intentionally inspired by the ADempiere / Compiere / Vienna Advantage dictionary model, but is implemented as a modern .NET/PostgreSQL/React platform.

---

# 2. Architecture Principles

1. **Metadata first.** Generic platform behavior must be driven by dictionary metadata wherever practical.
2. **Database as the metadata source of truth.**
3. **Generated code is disposable.** `X_<Table>` classes are generated and never hand-edited.
4. **Business rules belong in `M_<Table>` and registered validators/callouts.**
5. **Security is enforced server-side.** UI visibility is never the security boundary.
6. **All dynamic SQL identifiers are validated against metadata.**
7. **All values are parameterized.**
8. **Tenant and organization predicates are applied centrally.**
9. **Modules own their metadata and migrations and must be upgrade-safe.**
10. **Dictionary cache invalidation occurs only after successful metadata transactions.**
11. **Standard screens are generic; custom screens are explicit exceptions.**
12. **Document state and workflow state are separate concerns.**
13. **Backward-compatible migration is preferred over destructive replacement.**
14. **Every major platform capability must be testable independently and end-to-end.**

---

# 3. High-Level Architecture

```text
┌─────────────────────────────────────────────────────────────┐
│ React + TypeScript SPA                                     │
│                                                             │
│ Dynamic Window / Form / Grid / Lookup / Search / Menu      │
│ Designer / Workflow UI                                     │
└──────────────────────────────┬──────────────────────────────┘
                               │ HTTPS / JSON
┌──────────────────────────────▼──────────────────────────────┐
│ ASP.NET Core Web API                                       │
│                                                             │
│ Generic Data API                                            │
│ Metadata API                                                │
│ Lookup/Search API                                           │
│ Process API                                                 │
│ Authentication / Authorization / Tenant Context             │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│ Application / Domain Runtime                               │
│                                                             │
│ PO / X_<Table> / M_<Table>                                  │
│ Callouts / Model Validators                                 │
│ Document Engine / Workflow Engine / Process Engine          │
│ Access Control / Audit / Attachment / Sequence              │
│ Module Host / Registry                                      │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│ Metadata / Persistence                                      │
│                                                             │
│ Metadata Cache / Query Builder / Repository                 │
│ Reference Resolver / ValRule Engine / Expression Engine     │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│ PostgreSQL                                                  │
│                                                             │
│ Sys* Dictionary / Application Tables / Audit / Workflow     │
└─────────────────────────────────────────────────────────────┘
```

---

# 4. Technology Stack

| Area | Technology |
|---|---|
| Backend | .NET 8 |
| API | ASP.NET Core Web API |
| Frontend | React + TypeScript |
| Forms | React Hook Form |
| Data fetching | TanStack Query |
| Database | PostgreSQL 15+ |
| DB access | Dapper + Npgsql |
| Local metadata cache | IMemoryCache |
| Distributed cache | Redis |
| Background jobs | Hangfire or Quartz.NET |
| Messaging | RabbitMQ/Kafka when required |
| Plugin isolation | Collectible AssemblyLoadContext |
| Migration | DbUp or FluentMigrator |
| Authentication | ASP.NET Identity/JWT or external IdP |

Where an implementation choice is still open, the team must create an ADR before production adoption.

---

# 5. Core Domain Model

```text
SysElement
     │
SysColumn ───── SysReference ───── SysReferenceList
     │                 │
     │                 └────────── SysReferenceTable
     │
     └────────────── SysValRule
     │
SysTable
     │
     ├── SysWindow
     │      └── SysTab
     │            └── SysField ─── SysFieldGroup
     │
     ├── SysProcess
     │      └── SysProcessParam
     │
     └── SysWorkflow
            └── SysWfNode / SysWfProcess / SysWfActivity
```

---

# 6. Dictionary Architecture

The dictionary must separate four concepts:

### 6.1 Element

Reusable semantic definition:

- column name;
- label;
- description;
- help;
- translations.

### 6.2 Reference Type

Defines the fundamental field/control type, for example:

- String;
- Integer;
- Decimal;
- Date;
- DateTime;
- Yes/No;
- List;
- Table;
- Search;
- Text;
- Binary/Image.

### 6.3 Reference Value / Source

Defines the specific selectable value source:

- static list;
- table reference;
- search/reference definition.

### 6.4 Validation Rule

Defines the context-dependent subset of valid values.

This distinction is mandatory. A column's base type must not be confused with the specific lookup/reference definition or its runtime validation rule.

---

# 7. Database Schema

## 7.1 SysElement

```sql
CREATE TABLE SysElement (
    SysElement_ID SERIAL PRIMARY KEY,
    ColumnName VARCHAR(60) NOT NULL UNIQUE,
    Name VARCHAR(120) NOT NULL,
    Description VARCHAR(255),
    Help TEXT,
    IsActive BOOLEAN DEFAULT TRUE
);

CREATE TABLE SysElement_Trl (
    SysElement_ID INT NOT NULL REFERENCES SysElement(SysElement_ID),
    Language VARCHAR(10) NOT NULL,
    Name VARCHAR(120),
    Description VARCHAR(255),
    Help TEXT,
    PRIMARY KEY (SysElement_ID, Language)
);
```

## 7.2 SysReference

```sql
CREATE TABLE SysReference (
    SysReference_ID SERIAL PRIMARY KEY,
    Name VARCHAR(60) NOT NULL,
    ValidationType VARCHAR(10) NOT NULL,
    IsSystemType BOOLEAN DEFAULT FALSE,
    ValueFormat VARCHAR(60)
);

CREATE TABLE SysReferenceList (
    SysReferenceList_ID SERIAL PRIMARY KEY,
    SysReference_ID INT NOT NULL REFERENCES SysReference(SysReference_ID),
    Value VARCHAR(30) NOT NULL,
    Name VARCHAR(60) NOT NULL,
    SeqNo INT DEFAULT 0,
    IsActive BOOLEAN DEFAULT TRUE,
    UNIQUE(SysReference_ID, Value)
);

CREATE TABLE SysReferenceTable (
    SysReference_ID INT PRIMARY KEY REFERENCES SysReference(SysReference_ID),
    SysTable_ID INT NOT NULL REFERENCES SysTable(SysTable_ID),
    KeyColumn VARCHAR(60) NOT NULL,
    DisplayColumn VARCHAR(60) NOT NULL,
    WhereClause VARCHAR(500),
    OrderByClause VARCHAR(255)
);
```

## 7.3 SysValRule

```sql
CREATE TABLE SysValRule (
    SysValRule_ID SERIAL PRIMARY KEY,
    Name VARCHAR(120) NOT NULL,
    Description VARCHAR(255),
    RuleType VARCHAR(10) NOT NULL DEFAULT 'SQL',
    Code VARCHAR(2000) NOT NULL,
    IsActive BOOLEAN DEFAULT TRUE
);
```

## 7.4 SysTable / SysColumn

```sql
CREATE TABLE SysTable (
    SysTable_ID SERIAL PRIMARY KEY,
    TableName VARCHAR(60) NOT NULL UNIQUE,
    ClassName VARCHAR(120),
    Description VARCHAR(255),
    IsView BOOLEAN DEFAULT FALSE,
    AccessLevel SMALLINT DEFAULT 3,
    IsChangeLog BOOLEAN DEFAULT FALSE,
    IsDeleteable BOOLEAN DEFAULT TRUE,
    IsHighVolume BOOLEAN DEFAULT FALSE,
    ReplicationType VARCHAR(10) DEFAULT 'L',
    SysWindow_ID INT NULL,
    EntityType VARCHAR(20) DEFAULT 'D',
    IsActive BOOLEAN DEFAULT TRUE
);

CREATE TABLE SysColumn (
    SysColumn_ID SERIAL PRIMARY KEY,
    SysTable_ID INT NOT NULL REFERENCES SysTable(SysTable_ID),
    ColumnName VARCHAR(60) NOT NULL,
    SysElement_ID INT REFERENCES SysElement(SysElement_ID),
    SysReference_ID INT NOT NULL REFERENCES SysReference(SysReference_ID),
    SysReferenceValue_ID INT NULL,
    SysValRule_ID INT NULL REFERENCES SysValRule(SysValRule_ID),
    FieldLength INT,
    IsMandatory BOOLEAN DEFAULT FALSE,
    IsKey BOOLEAN DEFAULT FALSE,
    IsParent BOOLEAN DEFAULT FALSE,
    IsIdentifier BOOLEAN DEFAULT FALSE,
    IsSelectionColumn BOOLEAN DEFAULT FALSE,
    IsEncrypted BOOLEAN DEFAULT FALSE,
    IsUpdateable BOOLEAN DEFAULT TRUE,
    IsAlwaysUpdateable BOOLEAN DEFAULT FALSE,
    DefaultValue VARCHAR(255),
    ValueMin VARCHAR(60),
    ValueMax VARCHAR(60),
    SeqNo INT DEFAULT 0,
    EntityType VARCHAR(20) DEFAULT 'D',
    IsActive BOOLEAN DEFAULT TRUE,
    UNIQUE(SysTable_ID, ColumnName)
);
```

`SysReferenceValue_ID` must be implemented according to the reference-definition semantics established by the dictionary model; it must not be treated as another base data type.

---

# 8. UI Metadata

The UI dictionary must support:

- Window.
- Tab.
- Parent/detail relationship.
- Tab level.
- Field.
- Field group.
- Sequence.
- Display logic.
- Read-only state.
- Mandatory state.
- Same-line layout.
- Where clause.
- Order-by clause.
- Menu hierarchy.

Core tables:

```text
SysWindow
SysTab
SysField
SysFieldGroup
SysMenu
```

---

# 9. React Metadata Contract

Primary endpoint:

```http
GET /api/meta/window/{windowId}
```

Example:

```json
{
  "windowId": 100,
  "name": "Library Book",
  "tabs": [
    {
      "table": "library_book",
      "fields": [
        {
          "columnName": "Title",
          "label": "Title",
          "help": "Book title",
          "controlType": "TextInput",
          "isMandatory": true,
          "isReadOnly": false,
          "displayLogic": null
        }
      ]
    }
  ]
}
```

The frontend renderer must not contain business-specific field definitions.

Standard controls:

```text
TextInput
NumberInput
DateInput
YesNoToggle
ListDropdown
TableLookup
SearchPopup
TextArea
ImageUpload
```

---

# 10. Backend Object Model

```text
IPersistentObject
        │
        ▼
       PO
        │
        ▼
   X_<Table>
        │
        ▼
   M_<Table>
```

## PO responsibilities

- metadata lookup;
- value storage;
- dirty tracking;
- generic CRUD;
- mandatory validation;
- type validation;
- reference validation;
- ValRule validation;
- tenant/org assignment;
- audit hooks;
- lifecycle hooks.

## X_<Table>

Generated strongly typed accessors.

Rules:

- generated automatically;
- reproducible;
- never hand-edited.

## M_<Table>

Contains:

- business rules;
- domain methods;
- lifecycle overrides;
- document behavior;
- module-specific validation.

---

# 11. Metadata Validation Pipeline

```text
Incoming value
     │
     ▼
Column metadata
     │
     ├── Mandatory
     ├── Length
     ├── Min/Max
     ├── Base type
     ├── Reference
     └── ValRule
     │
     ▼
Model Validator / Callout
     │
     ▼
Business Rule
     │
     ▼
Persistence
```

Example:

```csharp
private void ValidateAgainstMetadata(MetaColumn col, object? value)
{
    if (col.IsMandatory && value is null)
        throw new BusinessRuleException(
            $"{col.ColumnName} is mandatory");

    TypeValidator.Validate(col.ReferenceType, value);

    if (col.ReferenceValueId is not null)
        ReferenceValueValidator.EnsureInSet(
            col.ReferenceValueId.Value,
            value);
}
```

---

# 12. Query Builder

All generic queries must follow this sequence:

```text
Request
  ↓
Validate table
  ↓
Validate requested columns
  ↓
Resolve metadata
  ↓
Apply tenant predicate
  ↓
Apply organization predicate
  ↓
Apply role/table/column access
  ↓
Apply record/private access
  ↓
Apply ValRule/context predicates
  ↓
Apply user filters
  ↓
Parameterize values
  ↓
Execute
```

No raw user-provided table names, column names, SQL fragments or untrusted expressions may be executed directly.

---

# 13. Generic API

```http
GET    /api/data/{table}
GET    /api/data/{table}/{id}
POST   /api/data/{table}
PUT    /api/data/{table}/{id}
DELETE /api/data/{table}/{id}

GET    /api/meta/window/{windowId}
GET    /api/lookup/{referenceId}
POST   /api/process/{processId}/run
```

The API must return consistent validation, authorization and business-rule errors.

Generic list endpoints must support pagination and controlled sorting/filtering.

---

# 14. Lookup Architecture

```text
React TableLookup
      ↓
GET /api/lookup/{referenceId}
      ↓
Resolve SysReference
      ↓
Resolve List/Table/Search source
      ↓
Apply ValRule
      ↓
Apply tenant/org security
      ↓
Apply role/record security
      ↓
Apply search text
      ↓
Parameterized SQL
      ↓
Return key + display value
```

High-volume tables must not be loaded as complete dropdowns; they must use search/popup lookup behavior.

---

# 15. Security and Tenancy

Security model:

```text
User
 │
 └── Role(s)
       ├── Client
       ├── Organization
       ├── Window
       ├── Process
       ├── Table
       ├── Column
       └── Record
```

Required security metadata:

```text
SysClient
SysOrg
SysUser
SysRole
SysUserRoles
SysRoleOrgAccess
SysUserOrgAccess
SysRoleWindowAccess
SysRoleProcessAccess
SysRoleTableAccess
SysRoleColumnAccess
SysRecordAccess
SysPrivateAccess
SysSession
```

Security checks must be applied centrally in the application/runtime layer.

PostgreSQL Row-Level Security may be added as defense-in-depth where appropriate, but it does not replace application authorization.

---

# 16. Process Engine

Core metadata:

```text
SysProcess
SysProcessParam
SysProcessInstance
```

Each process execution records:

- process;
- user;
- start time;
- end time;
- parameters;
- status;
- result message;
- error information.

Process access is independently authorized.

---

# 17. Workflow Engine

Workflow is separate from document status.

```text
SysWorkflow
    │
    ├── SysWfNode
    │      └── SysWfNodeNext
    │
    └── SysWfProcess
            └── SysWfActivity
```

Supported node categories:

- Human.
- Automatic.
- SubProcess.
- Wait.

Runtime flow:

```text
Record enters workflow
   ↓
Create workflow instance
   ↓
Resolve start node
   ↓
Create activity
   ↓
Execute / wait
   ↓
Evaluate transition
   ↓
Next node
   ↓
Complete
   ↓
Audit
```

---

# 18. Document Engine

Document processing must remain distinct from generic workflow.

Typical lifecycle:

```text
Draft
  ↓
Complete
  ↓
Approved / Rejected
  ↓
Processed
  ↓
Closed
```

`DocAction` and workflow transitions may interact, but they must not be represented as the same state machine.

---

# 19. Audit

`SysChangeLog` must support:

- table;
- record ID;
- column;
- old value;
- new value;
- user;
- timestamp.

A table opts into logging through dictionary metadata.

Audit writes must occur within the same logical transaction where consistency is required.

---

# 20. Attachments

Attachments are polymorphic:

```text
(SysTable_ID, Record_ID)
        ↓
      Files
```

The attachment abstraction must support:

- metadata;
- filename;
- MIME type;
- size;
- storage reference;
- uploader;
- timestamp;
- deletion policy.

The physical blob backend remains configurable.

---

# 21. Sequences

`SysSequence` provides controlled sequence generation for business/document numbering where PostgreSQL identity/sequence semantics alone are insufficient.

Requirements:

- transaction-safe allocation;
- tenant/client/org awareness where required;
- configurable prefix/suffix/format;
- no duplicate issued number;
- concurrency-safe implementation.

---

# 22. Trees

`SysTree` and `SysTreeNode` provide reusable hierarchical structures for:

- categories;
- organizations;
- menus;
- classifications;
- other hierarchical metadata.

Tree operations must validate parent relationships and prevent invalid cycles.

---

# 23. Code Generation

Initial implementation:

```text
Dictionary
   ↓
Generator
   ↓
SysTable
   ↓
SysColumn
   ↓
X_<Table>.cs
```

Generated code must be deterministic.

Future implementation may use Roslyn incremental source generation.

Business logic must never be placed in generated files.

---

# 24. Module Architecture

Module structure:

```text
LibraryModule/
 ├── manifest.json
 ├── migrations/
 │    ├── 001_create_library_book.sql
 │    └── 002_seed_dictionary.sql
 └── LibraryModule.Library.dll
```

Example:

```json
{
  "moduleId": "library-management",
  "version": "1.0.0",
  "dependsOn": [],
  "assembly": "LibraryModule.Library.dll",
  "migrationsPath": "migrations"
}
```

Installation:

```text
Read manifest
   ↓
Validate module ID/version
   ↓
Validate dependencies
   ↓
Apply migrations
   ↓
Seed/update dictionary
   ↓
Load assembly
   ↓
Register callouts/validators/processes/workflows
   ↓
Invalidate metadata cache
   ↓
Health check
   ↓
ACTIVE
```

Modules must not overwrite user-owned dictionary metadata.

---

# 25. Metadata Cache

Cache layers:

```text
Node Local Memory
       ↕
     Redis
       ↕
 PostgreSQL
```

Cache entries include:

- table metadata;
- column metadata;
- element metadata;
- reference metadata;
- window metadata;
- process metadata;
- workflow metadata;
- security metadata.

Dictionary mutation:

```text
Begin transaction
   ↓
Update metadata
   ↓
Commit
   ↓
Publish DictionaryChanged
   ↓
Invalidate local cache
   ↓
Invalidate distributed cache
```

Never invalidate all nodes before a failed transaction is committed.

---

# 26. End-to-End Create Record Flow

```text
React form
   ↓
POST /api/data/{table}
   ↓
Authenticate
   ↓
Resolve user/client/org/role
   ↓
Authorize table/columns/record
   ↓
Create PO
   ↓
Resolve M_<Table>
   ↓
Apply values
   ↓
Metadata validation
   ↓
Reference validation
   ↓
ValRule validation
   ↓
Callouts / model validators
   ↓
Business rules
   ↓
Set tenant/org/audit fields
   ↓
Parameterized INSERT
   ↓
Audit
   ↓
After-save hooks
   ↓
Response
```

---

# 27. End-to-End Update Flow

```text
Request
 ↓
Authenticate / authorize
 ↓
Load PO
 ↓
Check record access
 ↓
Track changed fields
 ↓
Validate writable columns
 ↓
Metadata validation
 ↓
ValRule validation
 ↓
Callouts / model validators
 ↓
M_<Table> business logic
 ↓
Parameterized UPDATE
 ↓
Audit old/new values
 ↓
Commit
 ↓
Response
```

---

# 28. End-to-End Delete Flow

```text
Request
 ↓
Authenticate
 ↓
Table delete permission
 ↓
Record permission
 ↓
Load object
 ↓
M_<Table> delete rule
 ↓
Reference/dependency validation
 ↓
Parameterized DELETE
 ↓
Audit
 ↓
Commit
```

Delete behavior must honor `IsDeleteable` and business/dependency rules.

---

# 29. End-to-End Dictionary Change Flow

```text
Designer/Admin
   ↓
Validate metadata
   ↓
Open transaction
   ↓
Write Sys* metadata
   ↓
Commit
   ↓
Publish DictionaryChangedEvent
   ↓
Invalidate node cache
   ↓
Invalidate Redis
   ↓
React requests fresh metadata
```

---

# 30. Non-Functional Requirements

## Security

- Parameterized SQL.
- Server-side authorization.
- Tenant isolation.
- Organization isolation.
- Column security.
- Record security.
- Session controls.
- Encrypted fields where required.
- Audit for protected operations.
- Safe plugin loading.

## Performance

- Cache dictionary metadata.
- Redis for multi-node cache invalidation.
- Pagination for generic queries.
- Search-based high-volume lookups.
- Proper indexes for tenant/org/reference columns.
- Batch processing for jobs.

## Reliability

- Transactional dictionary changes.
- Idempotent migrations.
- Versioned modules.
- Dependency validation.
- Rollback/upgrade strategy.
- Health checks.
- Process execution history.

## Observability

Track at minimum:

- API latency;
- DB latency;
- metadata cache hit/miss;
- module load failures;
- migration failures;
- process duration/failure;
- workflow failures;
- authorization denials;
- audit failures;
- cache invalidation failures.

---

# 31. Suggested Backend Solution Structure

```text
src/
 ├── Platform.Core/
 │    ├── PO/
 │    ├── Metadata/
 │    ├── Security/
 │    ├── Validation/
 │    ├── Query/
 │    └── Events/
 │
 ├── Platform.Persistence/
 │    ├── PostgreSQL/
 │    ├── Repositories/
 │    └── Migrations/
 │
 ├── Platform.Application/
 │    ├── DataApi/
 │    ├── MetadataApi/
 │    ├── LookupApi/
 │    ├── ProcessApi/
 │    └── Workflow/
 │
 ├── Platform.ModuleHost/
 │    ├── Manifest/
 │    ├── Loader/
 │    └── Registry/
 │
 ├── Platform.CodeGen/
 │    └── XClassGenerator/
 │
 ├── Platform.WebApi/
 │
 └── Modules/
      └── LibraryModule/
```

---

# 32. Suggested React Structure

```text
src/
 ├── metadata/
 ├── forms/
 ├── grids/
 ├── lookup/
 ├── menus/
 ├── workflows/
 ├── auth/
 ├── api/
 └── components/
```

---

# 33. Testing Strategy

## Unit tests

- Metadata resolver.
- Type validator.
- Reference validator.
- ValRule evaluator.
- Expression evaluator.
- Access-control evaluator.
- QueryBuilder.
- Sequence generator.

## Integration tests

- PostgreSQL CRUD.
- Tenant isolation.
- Organization isolation.
- Role access.
- Column access.
- Record access.
- Module installation.
- Migration.
- Cache invalidation.
- Process execution.
- Workflow transition.
- Audit.

## API contract tests

- Metadata API.
- Data API.
- Lookup API.
- Process API.

## End-to-end tests

A complete module must be installable and usable through standard dynamic screens without writing module-specific React screens for standard CRUD.

---

# 34. Migration Plan

Migration from the existing platform must be staged.

### Phase 1 — Dictionary foundation

- SysElement.
- Translations.
- Reference type/value separation.
- SysReferenceTable.
- SysValRule.

### Phase 2 — Runtime

- MetaColumn upgrade.
- Metadata cache graph.
- Type/reference validation.
- ValRule engine.
- PO lifecycle.

### Phase 3 — UI

- React metadata contract.
- Generic form.
- Generic grid.
- Lookup/search.
- Display logic.
- Menu/field groups.

### Phase 4 — Security

- Client/org model.
- Roles.
- Window/process/table/column access.
- Record/private access.
- Sessions.

### Phase 5 — Process/workflow

- Process instances.
- Scheduler integration.
- Workflow definitions.
- Workflow runtime.
- Document engine boundary.

### Phase 6 — Platform services

- Sequences.
- Trees.
- Audit.
- Attachments.
- Module/package tracking.

### Phase 7 — Production hardening

- Distributed cache invalidation.
- Upgrade/rollback.
- Security testing.
- Load testing.
- Observability.
- End-to-end module lifecycle tests.

---

# 35. 55 Implementation Changes

The following 55 items are the implementation backlog for reaching the target architecture.

## Dictionary / Metadata

1. Introduce the Element layer.
2. Separate reference type from reference value/source.
3. Add dynamic validation rules.
4. Add table-driven references.
5. Add translations.
6. Add field groups.
7. Add menu/navigation metadata.
8. Add search/info metadata.
9. Add report/print extensibility metadata.
10. Add dictionary entity ownership.

## Runtime

11. Upgrade `MetaColumn`.
12. Upgrade the metadata cache graph.
13. Add base type validation.
14. Add reference validation.
15. Add ValRule evaluation.
16. Add runtime context-variable resolution.
17. Strengthen PO validation.
18. Strengthen PO lifecycle hooks.
19. Strengthen PO factory/class resolution.
20. Establish the document-engine boundary.

## UI

21. Standardize React/TypeScript.
22. Define and version the metadata JSON contract.
23. Implement the generic form renderer.
24. Implement the generic grid.
25. Implement list/table/search lookup controls.
26. Implement search popup behavior.
27. Implement display-logic evaluation.
28. Implement field groups/layout metadata.
29. Implement menu renderer.
30. Provide a controlled custom-form escape hatch.

## Security

31. Add process access control.
32. Add record access control.
33. Add private record access.
34. Add role-organization access.
35. Add user-organization access.
36. Add session tracking.
37. Strengthen column-level projection/write filtering.
38. Strengthen server-side row predicate enforcement.
39. Add export/report permissions.
40. Add PostgreSQL RLS as optional defense-in-depth.

## Process / Workflow

41. Add process execution history.
42. Add scheduler metadata/integration.
43. Add workflow definitions.
44. Add workflow runtime.
45. Keep workflow state separate from document status.

## Infrastructure

46. Add sequences.
47. Add generic trees.
48. Add audit/change log.
49. Add generic attachments.
50. Add migration/module package tracking.

## Modules / DevOps

51. Version module manifests.
52. Validate module dependencies.
53. Enforce upgrade-safe dictionary ownership.
54. Add distributed dictionary-cache invalidation.
55. Add end-to-end install/upgrade/rollback tests.

---

# 36. Developer Acceptance Criteria

The implementation is considered aligned with this HLD/LLD when all of the following are true:

1. A new table can be registered in the dictionary.
2. Its columns can be registered with elements, references and validation rules.
3. Standard CRUD works without writing a custom API controller.
4. `X_<Table>` can be generated automatically.
5. `M_<Table>` can provide business rules without modifying generated code.
6. A standard React window can be rendered from metadata.
7. List/table/search references work generically.
8. Dynamic validation rules work with runtime context.
9. Client/org/role security is enforced server-side.
10. Window/process/table/column/record access is enforced.
11. Process executions are persisted.
12. Workflow instances and activities are persisted.
13. Audit records are generated when enabled.
14. Attachments can be associated with arbitrary records.
15. Generic trees and sequences work.
16. A module can install migrations and dictionary metadata.
17. Module dependencies and versions are validated.
18. Module-owned metadata cannot accidentally overwrite user-owned metadata.
19. Metadata cache invalidates correctly across application nodes.
20. The full module lifecycle is covered by automated tests.
21. Generic APIs reject unsafe table/column/filter input.
22. Tenant/org predicates cannot be bypassed by clients.
23. Standard modules require no custom React screen for standard CRUD.
24. Production observability is available for API, DB, metadata, process, workflow and module failures.

---

# 37. Mandatory ADRs Before Production

The team must explicitly decide:

1. Hangfire vs Quartz.NET.
2. RabbitMQ vs Kafka, if distributed messaging is required.
3. Redis topology.
4. Identity provider.
5. React component library.
6. Attachment/blob storage.
7. ValRule expression language.
8. Display-logic expression grammar.
9. PostgreSQL schema strategy.
10. Migration rollback strategy.

These are implementation choices and should not be silently decided by individual developers.

---

# 38. Final Implementation Rule

This document is the **target-state implementation specification**.

Developers should implement against the sections above rather than maintaining separate competing HLD/LLD versions.

Any deviation must be recorded as an ADR or architecture change request and must identify:

- affected database schema;
- affected backend classes/services;
- affected APIs;
- affected React metadata contract;
- security impact;
- migration impact;
- testing impact;
- backward-compatibility impact.

