# ADR-0009: UI Metadata Table Schema

- **ID**: ADR-0009
- **Status**: Proposed
- **Date**: 2026-08-15
- **Context**: Phase 3 needs UI metadata tables to store window, tab, field, field group, and menu definitions. These tables replace hardcoded screen layouts with metadata-driven configuration. The HLD/LLD specifies that UI structure is stored in the database and consumed by generic React components (GenericForm, GenericGrid). Phase 2 provides the MetadataGraph, MetadataCacheService, and cache invalidation infrastructure.

## Problem

How do we design a UI metadata schema that:
- Stores window/tab/field/field group/menu hierarchy
- Supports drag-and-drop UI builders (reordering via SeqNo)
- Allows cascade delete (tab deletion removes its fields)
- Prevents orphaned records
- Supports tenant-vs-module metadata ownership (EntityType)
- Is efficient for metadata graph queries (load window + tabs + fields in minimal round-trips)
- Has appropriate indexes for active-only queries (IsActive partial indexes)
- Has audit columns for change tracking
- Is idempotent for migrations (re-runnable DbUp scripts)

## Decision

Use a **5-table schema** with hierarchical parent-child relationships and snake_case naming:

### Table Overview

| Table | PK | FKs | Unique | Partial Index |
|---|---|---|---|---|
| sys_window | sys_window_id SERIAL | (none — default_tab_id removed) | (column_name) | is_active |
| sys_field_group | sys_field_group_id SERIAL | sys_tab_id → sys_tab CASCADE | (sys_tab_id, column_name) | is_active |
| sys_tab | sys_tab_id SERIAL | sys_window_id → sys_window CASCADE, sys_table_id → sys_table | (sys_window_id, column_name) | is_active |
| sys_field | sys_field_id SERIAL | sys_tab_id → sys_tab CASCADE, sys_column_id → sys_column, sys_field_group_id → sys_field_group | (sys_tab_id, sys_column_id) | is_active |
| sys_menu | sys_menu_id SERIAL | parent_id → sys_menu self-ref CASCADE, sys_window_id → sys_window, process_id | (column_name) | is_active |

### Detailed Schema

#### sys_window

Stores top-level window definitions (e.g., "Library Book", "User Management").

```sql
CREATE TABLE sys_window (
    sys_window_id   SERIAL PRIMARY KEY,
    column_name     VARCHAR(100) NOT NULL,       -- e.g., 'LibraryBook', 'UserManagement'
    display_name    VARCHAR(200) NOT NULL,       -- e.g., 'Library Book', 'User Management'
    description     VARCHAR(500),
    icon            VARCHAR(50),                 -- Ant Design icon name, e.g., 'BookOutlined'
    sort_order      INT NOT NULL DEFAULT 0,
    is_active       BOOLEAN NOT NULL DEFAULT true,
    entity_type     VARCHAR(10) NOT NULL DEFAULT 'D',  -- 'D' = platform, 'M' = module
    created_by      VARCHAR(100),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by      VARCHAR(100),
    updated_at      TIMESTAMPTZ
);

CREATE UNIQUE INDEX uq_sys_window_column_name ON sys_window (column_name);
CREATE INDEX idx_sys_window_is_active ON sys_window (is_active) WHERE is_active = true;
CREATE INDEX idx_sys_window_sort_order ON sys_window (sort_order);
```

#### sys_field_group

Stores collapsible field sections within tabs (e.g., "Basic Info", "Address", "Financial").

```sql
CREATE TABLE sys_field_group (
    sys_field_group_id  SERIAL PRIMARY KEY,
    sys_tab_id          INT NOT NULL REFERENCES sys_tab (sys_tab_id) ON DELETE CASCADE,
    column_name         VARCHAR(100) NOT NULL,   -- e.g., 'basicInfo', 'address'
    display_name        VARCHAR(200) NOT NULL,   -- e.g., 'Basic Info', 'Address'
    is_collapsible      BOOLEAN NOT NULL DEFAULT true,
    is_collapsed_by_default BOOLEAN NOT NULL DEFAULT false,
    col_span            INT NOT NULL DEFAULT 12, -- Ant Design grid col span (0-24)
    sort_order          INT NOT NULL DEFAULT 0,
    is_active           BOOLEAN NOT NULL DEFAULT true,
    entity_type         VARCHAR(10) NOT NULL DEFAULT 'D',
    created_by          VARCHAR(100),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by          VARCHAR(100),
    updated_at          TIMESTAMPTZ
);

CREATE UNIQUE INDEX uq_sys_field_group_tab_column ON sys_field_group (sys_tab_id, column_name);
CREATE INDEX idx_sys_field_group_tab ON sys_field_group (sys_tab_id);
CREATE INDEX idx_sys_field_group_sort_order ON sys_field_group (sort_order);
CREATE INDEX idx_sys_field_group_is_active ON sys_field_group (is_active) WHERE is_active = true;
```

#### sys_tab

Stores tabs within windows (e.g., "Main", "Details", "Audit Log").

```sql
CREATE TABLE sys_tab (
    sys_tab_id          SERIAL PRIMARY KEY,
    sys_window_id       INT NOT NULL REFERENCES sys_window (sys_window_id) ON DELETE CASCADE,
    sys_table_id        INT NOT NULL REFERENCES sys_table (sys_table_id),
    column_name         VARCHAR(100) NOT NULL,   -- e.g., 'main', 'details', 'grid'
    display_name        VARCHAR(200) NOT NULL,   -- e.g., 'Main', 'Details', 'Grid'
    is_default          BOOLEAN NOT NULL DEFAULT false,
    sort_order          INT NOT NULL DEFAULT 0,
    is_active           BOOLEAN NOT NULL DEFAULT true,
    entity_type         VARCHAR(10) NOT NULL DEFAULT 'D',
    created_by          VARCHAR(100),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by          VARCHAR(100),
    updated_at          TIMESTAMPTZ
);

CREATE UNIQUE INDEX uq_sys_tab_window_column ON sys_tab (sys_window_id, column_name);
CREATE INDEX idx_sys_tab_window ON sys_tab (sys_window_id);
CREATE INDEX idx_sys_tab_table ON sys_tab (sys_table_id);
CREATE INDEX idx_sys_tab_sort_order ON sys_tab (sort_order);
CREATE INDEX idx_sys_tab_is_active ON sys_tab (is_active) WHERE is_active = true;
```

#### sys_field

Stores field definitions within tabs — the core mapping between UI fields and data columns.

```sql
CREATE TABLE sys_field (
    sys_field_id            SERIAL PRIMARY KEY,
    sys_tab_id              INT NOT NULL REFERENCES sys_tab (sys_tab_id) ON DELETE CASCADE,
    sys_column_id           INT NOT NULL REFERENCES sys_column (sys_column_id),
    sys_field_group_id      INT REFERENCES sys_field_group (sys_field_group_id) ON DELETE CASCADE,
    column_name             VARCHAR(100) NOT NULL,   -- not used as unique — field uniqueness is per-tab
    display_name            VARCHAR(200),             -- override display name (null = use sys_column)
    control_type            VARCHAR(50),              -- 'input', 'select', 'datepicker', 'lookup', etc.
    col_span                INT NOT NULL DEFAULT 12,  -- Ant Design grid col span (0-24)
    row_span                INT NOT NULL DEFAULT 1,
    is_mandatory            BOOLEAN NOT NULL DEFAULT false,
    is_readonly             BOOLEAN NOT NULL DEFAULT false,
    is_visible              BOOLEAN NOT NULL DEFAULT true,
    display_logic           VARCHAR(500),             -- ADR-0006 expression
    read_only_logic         VARCHAR(500),             -- ADR-0006 expression
    mandatory_logic         VARCHAR(500),             -- ADR-0006 expression
    placeholder             VARCHAR(200),
    sort_order              INT NOT NULL DEFAULT 0,
    is_active               BOOLEAN NOT NULL DEFAULT true,
    entity_type             VARCHAR(10) NOT NULL DEFAULT 'D',
    created_by              VARCHAR(100),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by              VARCHAR(100),
    updated_at              TIMESTAMPTZ
);

CREATE UNIQUE INDEX uq_sys_field_tab_column ON sys_field (sys_tab_id, sys_column_id);
CREATE INDEX idx_sys_field_tab ON sys_field (sys_tab_id);
CREATE INDEX idx_sys_field_column ON sys_field (sys_column_id);
CREATE INDEX idx_sys_field_group ON sys_field (sys_field_group_id);
CREATE INDEX idx_sys_field_sort_order ON sys_field (sort_order);
CREATE INDEX idx_sys_field_is_active ON sys_field (is_active) WHERE is_active = true;
```

#### sys_menu

Stores hierarchical navigation menu definitions.

```sql
CREATE TABLE sys_menu (
    sys_menu_id     SERIAL PRIMARY KEY,
    parent_id       INT REFERENCES sys_menu (sys_menu_id) ON DELETE CASCADE,
    sys_window_id   INT REFERENCES sys_window (sys_window_id),
    process_id      INT,                                  -- links to a process/workflow if applicable
    column_name     VARCHAR(100) NOT NULL,                -- unique identifier
    display_name    VARCHAR(200) NOT NULL,                -- e.g., 'Books', 'Users', 'Reports'
    icon            VARCHAR(50),                          -- Ant Design icon name
    route           VARCHAR(500),                         -- e.g., '/library-book'
    sort_order      INT NOT NULL DEFAULT 0,
    is_active       BOOLEAN NOT NULL DEFAULT true,
    entity_type     VARCHAR(10) NOT NULL DEFAULT 'D',
    created_by      VARCHAR(100),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by      VARCHAR(100),
    updated_at      TIMESTAMPTZ
);

CREATE UNIQUE INDEX uq_sys_menu_column ON sys_menu (column_name);
CREATE INDEX idx_sys_menu_parent ON sys_menu (parent_id);
CREATE INDEX idx_sys_menu_window ON sys_menu (sys_window_id);
CREATE INDEX idx_sys_menu_sort_order ON sys_menu (sort_order);
CREATE INDEX idx_sys_menu_is_active ON sys_menu (is_active) WHERE is_active = true;
```

## Key Design Decisions

### 1. No Tenant Column on UI Metadata

UI structure is shared across tenants. Tenant-specific customization happens at the Phase 4+ layer (e.g., per-tenant window visibility, role-based column access).

**Why**: UI metadata defines the structure, not the data. Tenants share the same form/grid structures but see different data.

### 2. Cascade Delete on sys_tab → sys_field

Deleting a tab cascades to its fields. Deleting a window cascades to its tabs (and transitively to fields).

**Why**: Prevents orphaned fields. A field without a tab is meaningless. Cascade is simpler and safer than soft-delete cascades.

### 3. sys_window.default_tab_id FK Removed

The FK from sys_window.default_tab_id to sys_tab(sys_tab_id) was removed because it creates a circular dependency: sys_tab has sys_window_id FK, and sys_window would need sys_tab_id FK — impossible to seed.

**Alternative**: Application-level validation ensures default_tab_id references a tab belonging to the same window.

### 4. EntityType ('D' / 'M')

- `'D'` (dictionary/platform): Platform-owned metadata. Cannot be overwritten by module upgrades.
- `'M'` (module): Module-owned metadata. Module upgrades can update its own metadata.

**Why**: Prevents module upgrades from overwriting user-customized UI metadata.

### 5. IsActive Partial Indexes

```sql
CREATE INDEX idx_sys_window_is_active ON sys_window (is_active) WHERE is_active = true;
```

All queries default to active-only. Partial indexes are smaller and faster than full indexes with `WHERE is_active = true`.

**Why**: Hides records without adding WHERE clause overhead. Soft-deleted records (is_active=false) don't appear in default queries.

### 6. Snake_case Naming

All table and column names use snake_case, consistent with the rest of the platform's database convention.

### 7. Audit Columns on All Tables

Every table has `created_by`, `created_at`, `updated_by`, `updated_at`.

**Why**: Change tracking for UI metadata. Who changed the form layout? When was a field made mandatory?

### 8. SeqNo Composite Indexes

All tables have indexes on `(sort_order)` and composite indexes like `(sys_tab_id, sort_order)`.

**Why**: Ordered rendering — tabs and fields loaded in display order with a single query.

## MetadataGraph Query Patterns

The MetadataGraph loads UI metadata for window construction:

```sql
-- Load window + all related data in 4 queries (minimal round-trips)

-- 1. Window
SELECT * FROM sys_window WHERE sys_window_id = @id AND is_active = true;

-- 2. Tabs + Field Groups (per window)
SELECT t.*, fg.* FROM sys_tab t
LEFT JOIN sys_field_group fg ON fg.sys_tab_id = t.sys_tab_id AND fg.is_active = true
WHERE t.sys_window_id = @windowId AND t.is_active = true
ORDER BY t.sort_order, fg.sort_order;

-- 3. Fields (per tab)
SELECT f.*, c.*, e.*, r.*
FROM sys_field f
JOIN sys_column c ON c.sys_column_id = f.sys_column_id
LEFT JOIN sys_element e ON e.sys_element_id = c.sys_element_id
LEFT JOIN sys_reference r ON r.sys_reference_id = c.sys_reference_id
WHERE f.sys_tab_id = @tabId AND f.is_active = true
ORDER BY f.sort_order;

-- 4. Menu hierarchy
SELECT * FROM sys_menu
WHERE sys_window_id = @windowId AND is_active = true
ORDER BY sort_order;
```

Total: 4 queries per window metadata build. Cached in IMemoryCache (5 min TTL).

## Migration 003

- **File**: `M003_Create_UI_Metadata_Tables.sql`
- **Embedded resource** in Platform.API
- **Runs after**: M002 (if any)
- **Idempotent**: `DO $$...$$` guards, `ON CONFLICT DO NOTHING` for seed data
- **Forward-only**: DbUp has no automatic rollback
- **Rollback**: Manual procedure documented in migration design doc

### Seed Data (Optional)

Seed a sample window for development:

```sql
INSERT INTO sys_window (column_name, display_name, icon, sort_order, is_active)
VALUES ('SampleBook', 'Sample Book', 'BookOutlined', 1, true)
ON CONFLICT (column_name) DO NOTHING;
```

## FK Indexing Notes

- All FK columns have indexes for cascade performance.
- Deferred indexes: SysColumn.SysTable_ID, SysReferenceList.SysReference_ID (from Phase 2) — not directly related to UI metadata but noted in Phase 2 warnings.
- UI metadata FKs all have indexes in the DDL above.

## Backward Compatibility

- N/A — new tables, no existing data affected.
- Nullable columns (description, placeholder, control_type) allow progressive enrichment.
- Default `is_active = true` means all new records are visible by default.

## Testing Implications

- **Schema contract tests**: Verify all 5 tables exist, have expected columns, types, PKs, FKs, indexes
- **Constraint tests**: Verify cascade delete (tab delete removes fields), unique constraints (duplicate column_name rejected), not-null constraints
- **Seed data tests**: Verify seed data is idempotent (run migration twice → same result)
- **Query tests**: Verify MetadataGraph loads correct data in 4 queries

## Security Implications

- **No user input in schema**: Table/column names come from metadata, not user input.
- **EntityType enforcement**: Application code must check entity_type before updating — not a database constraint.
- **Audit columns**: Populated by application code (from IReadOnlyContext.UserId), not database defaults. Null in Phase 3 (no auth).

## References

- HLD/LLD Section 8: UI Metadata — Window, Tab, Field, Menu definitions
- HLD/LLD Section 34, Item 29: Implement generic form/grid metadata consumption
- Migration 003 Design: `docs/database/PHASE-3-MIGRATION-003-DESIGN.md`
- ADR-0006: Display Logic Grammar (uses sys_field.DisplayLogic column)
- CLAUDE.md Rule 10: Module-owned metadata must be upgrade-safe
