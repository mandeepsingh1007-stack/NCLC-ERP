# Migration 003 — UI Metadata Tables Design

**Phase:** 3 — UI (Generic Forms, Grids, Lookups, Menus)
**Date:** 2026-08-15
**Status:** Design — awaiting review
**Author:** Design Closure (Orchestrator)

---

## 1. Overview

Migration 003 creates 5 UI metadata tables required by Phase 3 for metadata-driven generic forms and grids:

| Table | Purpose |
|---|---|
| `SysWindow` | Full-screen window definitions |
| `SysTab` | Tabs within windows, bound to data tables |
| `SysField` | Field rendering within tabs, mapped to data columns |
| `SysFieldGroup` | Field grouping within tabs (collapsible sections) |
| `SysMenu` | Navigation menu hierarchy |

**DbUp file:** `M003_Create_UI_Metadata_Tables.sql`
**Embedded as:** Embedded resource in `Platform.API` assembly
**Order:** Runs after M002 (Phase 2 migration, if any)
**Idempotent:** YES — uses `DO $$...$$` guard for safety

---

## 2. Tables

### 2.1 SysWindow

```sql
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables
                   WHERE table_schema = 'public'
                     AND table_name = 'sys_window') THEN

        CREATE TABLE public.sys_window (
            sys_window_id           SERIAL       PRIMARY KEY,
            column_name             VARCHAR(60)  NOT NULL,
            name                    VARCHAR(120) NOT NULL,
            description             VARCHAR(255),
            help                    TEXT,
            default_tab_id          INT,
            access_level            SMALLINT     NOT NULL DEFAULT 3,
            is_view                 BOOLEAN      NOT NULL DEFAULT FALSE,
            entity_type             VARCHAR(20)  NOT NULL DEFAULT 'D',
            is_active               BOOLEAN      NOT NULL DEFAULT TRUE,
            created_by              INT,
            created_at              TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
            updated_by              INT,
            updated_at              TIMESTAMPTZ  NOT NULL DEFAULT NOW()
        );

        ALTER TABLE public.sys_window
            ADD CONSTRAINT fk_sys_window_default_tab
                FOREIGN KEY (default_tab_id)
                REFERENCES public.sys_tab(sys_tab_id);

        ALTER TABLE public.sys_window
            ADD CONSTRAINT uq_sys_window_column_name
                UNIQUE (column_name);

        CREATE INDEX ix_sys_window_is_active
            ON public.sys_window(is_active) WHERE is_active = TRUE;

    END IF;
END $$;
```

**Constraints:**
- PK: `sys_window_id`
- FK: `default_tab_id` → `sys_tab(sys_tab_id)` — nullable (window may not have a default tab)
- UNIQUE: `column_name` (linked to SysElement.ColumnName)
- Index: `ix_sys_window_is_active` (partial index for active-only queries)

**Column details:**

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| sys_window_id | SERIAL | NO | auto | PK |
| column_name | VARCHAR(60) | NO | — | UNIQUE, linked to SysElement |
| name | VARCHAR(120) | NO | — | Display name |
| description | VARCHAR(255) | YES | — | |
| help | TEXT | YES | — | Help text for UI |
| default_tab_id | INT | YES | NULL | FK to sys_tab |
| access_level | SMALLINT | NO | 3 | 1=All, 2=Client, 3=Org, 4=Private |
| is_view | BOOLEAN | NO | FALSE | |
| entity_type | VARCHAR(20) | NO | 'D' | D=User, M=Module |
| is_active | BOOLEAN | NO | TRUE | Soft delete |
| created_by | INT | YES | NULL | Audit |
| created_at | TIMESTAMPTZ | NO | NOW() | Audit |
| updated_by | INT | YES | NULL | Audit |
| updated_at | TIMESTAMPTZ | NO | NOW() | Audit |

---

### 2.2 SysTab

```sql
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables
                   WHERE table_schema = 'public'
                     AND table_name = 'sys_tab') THEN

        CREATE TABLE public.sys_tab (
            sys_tab_id              SERIAL       PRIMARY KEY,
            sys_window_id           INT          NOT NULL,
            sys_table_id            INT          NOT NULL,
            column_name             VARCHAR(60)  NOT NULL,
            name                    VARCHAR(120) NOT NULL,
            seq_no                  INT          NOT NULL DEFAULT 0,
            is_default_tab          BOOLEAN      NOT NULL DEFAULT FALSE,
            is_grid                 BOOLEAN      NOT NULL DEFAULT FALSE,
            where_clause            VARCHAR(500),
            is_deleteable           BOOLEAN      NOT NULL DEFAULT TRUE,
            entity_type             VARCHAR(20)  NOT NULL DEFAULT 'D',
            is_active               BOOLEAN      NOT NULL DEFAULT TRUE,
            created_by              INT,
            created_at              TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
            updated_by              INT,
            updated_at              TIMESTAMPTZ  NOT NULL DEFAULT NOW()
        );

        ALTER TABLE public.sys_tab
            ADD CONSTRAINT fk_sys_tab_window
                FOREIGN KEY (sys_window_id)
                REFERENCES public.sys_window(sys_window_id)
                ON DELETE CASCADE;

        ALTER TABLE public.sys_tab
            ADD CONSTRAINT fk_sys_tab_table
                FOREIGN KEY (sys_table_id)
                REFERENCES public.sys_table(sys_table_id);

        ALTER TABLE public.sys_tab
            ADD CONSTRAINT uq_sys_tab_window_column
                UNIQUE (sys_window_id, column_name);

        CREATE INDEX ix_sys_tab_window
            ON public.sys_tab(sys_window_id);

        CREATE INDEX ix_sys_tab_table
            ON public.sys_tab(sys_table_id);

        CREATE INDEX ix_sys_tab_is_active
            ON public.sys_tab(is_active) WHERE is_active = TRUE;

        CREATE INDEX ix_sys_tab_seq_no
            ON public.sys_tab(sys_window_id, seq_no);

    END IF;
END $$;
```

**Constraints:**
- PK: `sys_tab_id`
- FK: `sys_window_id` → `sys_window(sys_window_id)` ON DELETE CASCADE
- FK: `sys_table_id` → `sys_table(sys_table_id)`
- UNIQUE: `(sys_window_id, column_name)`
- Indexes: FK indexes + partial active index + seq_no ordering index

**Column details:**

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| sys_tab_id | SERIAL | NO | auto | PK |
| sys_window_id | INT | NO | — | FK to sys_window |
| sys_table_id | INT | NO | — | FK to sys_table — bound to data table |
| column_name | VARCHAR(60) | NO | — | Tab identifier |
| name | VARCHAR(120) | NO | — | Display name |
| seq_no | INT | NO | 0 | Tab ordering within window |
| is_default_tab | BOOLEAN | NO | FALSE | Default selected tab |
| is_grid | BOOLEAN | NO | FALSE | Tab renders as grid vs form |
| where_clause | VARCHAR(500) | YES | NULL | Tab-level row filter (parameterized) |
| is_deleteable | BOOLEAN | NO | TRUE | |
| entity_type | VARCHAR(20) | NO | 'D' | D=User, M=Module |
| is_active | BOOLEAN | NO | TRUE | Soft delete |
| created_by | INT | YES | NULL | Audit |
| created_at | TIMESTAMPTZ | NO | NOW() | Audit |
| updated_by | INT | YES | NULL | Audit |
| updated_at | TIMESTAMPTZ | NO | NOW() | Audit |

---

### 2.3 SysField

```sql
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables
                   WHERE table_schema = 'public'
                     AND table_name = 'sys_field') THEN

        CREATE TABLE public.sys_field (
            sys_field_id            SERIAL       PRIMARY KEY,
            sys_tab_id              INT          NOT NULL,
            sys_column_id           INT          NOT NULL,
            column_name             VARCHAR(60)  NOT NULL,
            name                    VARCHAR(120) NOT NULL,
            control_type            VARCHAR(30)  NOT NULL,
            sys_field_group_id      INT,
            seq_no                  INT          NOT NULL DEFAULT 0,
            is_mandatory_override   BOOLEAN      NOT NULL DEFAULT FALSE,
            is_read_only_override   BOOLEAN      NOT NULL DEFAULT FALSE,
            col_span                INT          NOT NULL DEFAULT 1,
            row_span                INT          NOT NULL DEFAULT 1,
            display_logic           VARCHAR(500),
            read_only_logic         VARCHAR(500),
            mandatory_logic         VARCHAR(500),
            default_value           VARCHAR(255),
            entity_type             VARCHAR(20)  NOT NULL DEFAULT 'D',
            is_active               BOOLEAN      NOT NULL DEFAULT TRUE,
            created_by              INT,
            created_at              TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
            updated_by              INT,
            updated_at              TIMESTAMPTZ  NOT NULL DEFAULT NOW()
        );

        ALTER TABLE public.sys_field
            ADD CONSTRAINT fk_sys_field_tab
                FOREIGN KEY (sys_tab_id)
                REFERENCES public.sys_tab(sys_tab_id)
                ON DELETE CASCADE;

        ALTER TABLE public.sys_field
            ADD CONSTRAINT fk_sys_field_column
                FOREIGN KEY (sys_column_id)
                REFERENCES public.sys_column(sys_column_id);

        ALTER TABLE public.sys_field
            ADD CONSTRAINT fk_sys_field_group
                FOREIGN KEY (sys_field_group_id)
                REFERENCES public.sys_field_group(sys_field_group_id);

        ALTER TABLE public.sys_field
            ADD CONSTRAINT uq_sys_field_tab_column
                UNIQUE (sys_tab_id, sys_column_id);

        CREATE INDEX ix_sys_field_tab
            ON public.sys_field(sys_tab_id);

        CREATE INDEX ix_sys_field_column
            ON public.sys_field(sys_column_id);

        CREATE INDEX ix_sys_field_group
            ON public.sys_field(sys_field_group_id);

        CREATE INDEX ix_sys_field_is_active
            ON public.sys_field(is_active) WHERE is_active = TRUE;

        CREATE INDEX ix_sys_field_seq_no
            ON public.sys_field(sys_tab_id, seq_no);

    END IF;
END $$;
```

**Constraints:**
- PK: `sys_field_id`
- FK: `sys_tab_id` → `sys_tab(sys_tab_id)` ON DELETE CASCADE
- FK: `sys_column_id` → `sys_column(sys_column_id)` — ensures field maps to real data column
- FK: `sys_field_group_id` → `sys_field_group(sys_field_group_id)` — nullable (ungrouped fields)
- UNIQUE: `(sys_tab_id, sys_column_id)` — one field definition per column per tab
- Indexes: FK indexes + partial active index + seq_no ordering index

**ControlType allowlist (documented in ADR-0005):**
`TextInput`, `NumberInput`, `DateInput`, `YesNoToggle`, `ListDropdown`, `TableLookup`, `SearchPopup`, `TextArea`, `ImageUpload`

**Column details:**

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| sys_field_id | SERIAL | NO | auto | PK |
| sys_tab_id | INT | NO | — | FK to sys_tab |
| sys_column_id | INT | NO | — | FK to sys_column — data column mapping |
| column_name | VARCHAR(60) | NO | — | Copy of column name for display clarity |
| name | VARCHAR(120) | NO | — | Display label |
| control_type | VARCHAR(30) | NO | — | React control type |
| sys_field_group_id | INT | YES | NULL | FK to field group |
| seq_no | INT | NO | 0 | Field ordering within tab |
| is_mandatory_override | BOOLEAN | NO | FALSE | Override column-level mandatory |
| is_read_only_override | BOOLEAN | NO | FALSE | Override column-level updateable |
| col_span | INT | NO | 1 | Responsive grid column span (1-12) |
| row_span | INT | NO | 1 | Row span (typically 1) |
| display_logic | VARCHAR(500) | YES | NULL | Conditional visibility expression |
| read_only_logic | VARCHAR(500) | YES | NULL | Conditional read-only expression |
| mandatory_logic | VARCHAR(500) | YES | NULL | Conditional mandatory expression |
| default_value | VARCHAR(255) | YES | NULL | Field-level default |
| entity_type | VARCHAR(20) | NO | 'D' | D=User, M=Module |
| is_active | BOOLEAN | NO | TRUE | Soft delete |
| created_by | INT | YES | NULL | Audit |
| created_at | TIMESTAMPTZ | NO | NOW() | Audit |
| updated_by | INT | YES | NULL | Audit |
| updated_at | TIMESTAMPTZ | NO | NOW() | Audit |

---

### 2.4 SysFieldGroup

```sql
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables
                   WHERE table_schema = 'public'
                     AND table_name = 'sys_field_group') THEN

        CREATE TABLE public.sys_field_group (
            sys_field_group_id    SERIAL       PRIMARY KEY,
            sys_tab_id            INT          NOT NULL,
            column_name           VARCHAR(60)  NOT NULL,
            name                  VARCHAR(120) NOT NULL,
            seq_no                INT          NOT NULL DEFAULT 0,
            col_span              INT          NOT NULL DEFAULT 12,
            is_collapsed          BOOLEAN      NOT NULL DEFAULT FALSE,
            entity_type           VARCHAR(20)  NOT NULL DEFAULT 'D',
            is_active             BOOLEAN      NOT NULL DEFAULT TRUE,
            created_by            INT,
            created_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
            updated_by            INT,
            updated_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW()
        );

        ALTER TABLE public.sys_field_group
            ADD CONSTRAINT fk_sys_field_group_tab
                FOREIGN KEY (sys_tab_id)
                REFERENCES public.sys_tab(sys_tab_id)
                ON DELETE CASCADE;

        ALTER TABLE public.sys_field_group
            ADD CONSTRAINT uq_sys_field_group_tab_column
                UNIQUE (sys_tab_id, column_name);

        CREATE INDEX ix_sys_field_group_tab
            ON public.sys_field_group(sys_tab_id);

        CREATE INDEX ix_sys_field_group_is_active
            ON public.sys_field_group(is_active) WHERE is_active = TRUE;

        CREATE INDEX ix_sys_field_group_seq_no
            ON public.sys_field_group(sys_tab_id, seq_no);

    END IF;
END $$;
```

**Column details:**

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| sys_field_group_id | SERIAL | NO | auto | PK |
| sys_tab_id | INT | NO | — | FK to sys_tab |
| column_name | VARCHAR(60) | NO | — | Group identifier |
| name | VARCHAR(120) | NO | — | Display name |
| seq_no | INT | NO | 0 | Group ordering |
| col_span | INT | NO | 12 | Section width (1-12) |
| is_collapsed | BOOLEAN | NO | FALSE | Default collapsed state |
| entity_type | VARCHAR(20) | NO | 'D' | D=User, M=Module |
| is_active | BOOLEAN | NO | TRUE | Soft delete |
| created_by | INT | YES | NULL | Audit |
| created_at | TIMESTAMPTZ | NO | NOW() | Audit |
| updated_by | INT | YES | NULL | Audit |
| updated_at | TIMESTAMPTZ | NO | NOW() | Audit |

---

### 2.5 SysMenu

```sql
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables
                   WHERE table_schema = 'public'
                     AND table_name = 'sys_menu') THEN

        CREATE TABLE public.sys_menu (
            sys_menu_id           SERIAL       PRIMARY KEY,
            parent_id             INT,
            column_name           VARCHAR(60)  NOT NULL,
            name                  VARCHAR(120) NOT NULL,
            icon                  VARCHAR(60),
            sequence              INT          NOT NULL DEFAULT 0,
            window_id             INT,
            process_id            INT,
            is_separator          BOOLEAN      NOT NULL DEFAULT FALSE,
            is_system             BOOLEAN      NOT NULL DEFAULT FALSE,
            entity_type           VARCHAR(20)  NOT NULL DEFAULT 'D',
            is_active             BOOLEAN      NOT NULL DEFAULT TRUE,
            created_by            INT,
            created_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
            updated_by            INT,
            updated_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW()
        );

        ALTER TABLE public.sys_menu
            ADD CONSTRAINT fk_sys_menu_parent
                FOREIGN KEY (parent_id)
                REFERENCES public.sys_menu(sys_menu_id);

        ALTER TABLE public.sys_menu
            ADD CONSTRAINT fk_sys_menu_window
                FOREIGN KEY (window_id)
                REFERENCES public.sys_window(sys_window_id);

        ALTER TABLE public.sys_menu
            ADD CONSTRAINT fk_sys_menu_process
                FOREIGN KEY (process_id)
                REFERENCES public.sys_process(sys_process_id);

        ALTER TABLE public.sys_menu
            ADD CONSTRAINT uq_sys_menu_column_name
                UNIQUE (column_name);

        CREATE INDEX ix_sys_menu_parent
            ON public.sys_menu(parent_id);

        CREATE INDEX ix_sys_menu_is_active
            ON public.sys_menu(is_active) WHERE is_active = TRUE;

        CREATE INDEX ix_sys_menu_sequence
            ON public.sys_menu(sequence);

        CREATE INDEX ix_sys_menu_window
            ON public.sys_menu(window_id);

    END IF;
END $$;
```

**Constraints:**
- PK: `sys_menu_id`
- FK: `parent_id` → `sys_menu(sys_menu_id)` — self-referencing hierarchy
- FK: `window_id` → `sys_window(sys_window_id)` — menu item opens a window
- FK: `process_id` → `sys_process(sys_process_id)` — menu item runs a process
- UNIQUE: `column_name`
- **Cycle prevention:** Application-level validation required on CREATE/UPDATE of parent_id. Do NOT use a CHECK constraint (circular). Validate in the metadata API that prevents setting parent_id to self or any descendant.

**Column details:**

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| sys_menu_id | SERIAL | NO | auto | PK |
| parent_id | INT | YES | NULL | Self-referencing FK |
| column_name | VARCHAR(60) | NO | — | UNIQUE identifier |
| name | VARCHAR(120) | NO | — | Display name |
| icon | VARCHAR(60) | YES | NULL | Icon reference name |
| sequence | INT | NO | 0 | Menu order |
| window_id | INT | YES | NULL | FK to sys_window |
| process_id | INT | YES | NULL | FK to sys_process |
| is_separator | BOOLEAN | NO | FALSE | Visual separator, not clickable |
| is_system | BOOLEAN | NO | FALSE | System menu (not user-editable) |
| entity_type | VARCHAR(20) | NO | 'D' | D=User, M=Module |
| is_active | BOOLEAN | NO | TRUE | Soft delete |
| created_by | INT | YES | NULL | Audit |
| created_at | TIMESTAMPTZ | NO | NOW() | Audit |
| updated_by | INT | YES | NULL | Audit |
| updated_at | TIMESTAMPTZ | NO | NOW() | Audit |

---

## 3. Naming Conventions

All table and column names follow **snake_case** convention (matching existing Phase 1 tables).

| HLD/LLD | Database |
|---|---|
| SysWindow | `sys_window` |
| SysTab | `sys_tab` |
| SysField | `sys_field` |
| SysFieldGroup | `sys_field_group` |
| SysMenu | `sys_menu` |

Column names: snake_case with `_id` suffix for FK columns (e.g., `sys_window_id`, `sys_table_id`).

---

## 4. Index Strategy

### Partial indexes for active-only queries

All tables use `WHERE is_active = TRUE` partial indexes. This is more efficient than filtering in every query because:
- PostgreSQL can skip inactive rows entirely
- Index size is smaller (only active rows indexed)
- Active-only is the dominant query pattern (inactive = deleted)

### FK indexes

Every FK column has a B-tree index for efficient JOINs. This was noted as a Phase 2 gap and is addressed here.

### SeqNo indexes

Composite indexes on `(parent_id, seq_no)` for ordered rendering of tabs, fields, and menu items within a parent.

---

## 5. Seed Data

Seed data is optional for Migration 003. If included:

```sql
-- Sample window for testing (library_book example from HLD/LLD)
INSERT INTO public.sys_window (column_name, name, description, access_level, entity_type)
VALUES ('window_library_book', 'Library Book', 'Manage library book records', 3, 'D')
ON CONFLICT (column_name) DO NOTHING;
```

Seed data:
- Uses `ON CONFLICT DO NOTHING` for idempotency
- `entity_type = 'D'` (platform-owned)
- Minimal — just enough to test generic form rendering

---

## 6. Tenant/Client/Org Relationship

**Decision:** UI metadata tables do NOT have tenant/client/org columns.

**Rationale:** The UI structure (windows, tabs, fields, menus) is SHARED across all tenants. Tenants see different DATA, but the same UI structure. Tenant isolation applies to data queries, not to UI metadata.

Phase 4 role-based access control will control WHICH windows a user can access via a separate permission table (not in this migration).

---

## 7. Audit Columns

All 5 tables include:
- `created_by INT` — user who created the metadata
- `created_at TIMESTAMPTZ` — creation timestamp
- `updated_by INT` — user who last modified
- `updated_at TIMESTAMPTZ` — last modification timestamp

These support audit trail (Phase 6 `SysChangeLog`) and are consistent with platform conventions.

---

## 8. Rollback / Recovery

### DbUp rollback

DbUp does not support automatic rollback. Migration 003 is forward-only.

### Manual rollback procedure

If Migration 003 fails and needs rollback:

```sql
-- Drop in reverse dependency order
DROP TABLE IF EXISTS public.sys_field CASCADE;
DROP TABLE IF EXISTS public.sys_field_group CASCADE;
DROP TABLE IF EXISTS public.sys_tab CASCADE;
DROP TABLE IF EXISTS public.sys_window CASCADE;
DROP TABLE IF EXISTS public.sys_menu CASCADE;
```

CASCADE ensures FK references are cleaned up. DbUp will re-run the migration on next startup because the migration marker was not recorded (failed migrations are not marked as successful).

---

## 9. Verification

After migration, verify:

1. All 5 tables exist in `information_schema.tables`
2. All FK constraints exist and reference correct tables
3. All UNIQUE constraints exist
4. All indexes exist
5. Partial indexes have correct `WHERE is_active = TRUE`
6. Seed data idempotent (run migration twice, same result)

---

## 10. Schema Diagram (text)

```
sys_window
    ├──→ sys_tab (FK: default_tab_id)
    │       ├──→ sys_field (FK: sys_tab_id)
    │       │       ├──→ sys_column (FK: sys_column_id)
    │       │       └──→ sys_field_group (FK: sys_field_group_id)
    │       └──→ sys_table (FK: sys_table_id)
    │
    └──→ sys_menu (FK: window_id)

sys_menu
    ├──→ sys_menu (self-FK: parent_id)
    └──→ sys_process (FK: process_id)
```

---

## 11. C# Domain Model Mapping

| Database Table | C# Class | Namespace |
|---|---|---|
| sys_window | SysWindow | Platform.Core.Metadata |
| sys_tab | SysTab | Platform.Core.Metadata |
| sys_field | SysField | Platform.Core.Metadata |
| sys_field_group | SysFieldGroup | Platform.Core.Metadata |
| sys_menu | SysMenu | Platform.Core.Metadata |

These classes mirror the existing metadata model (`MetaColumn`, `SysTable`, etc.) and are consumed by:
- Dapper repositories (data access)
- MetadataGraph (metadata loading)
- Meta API (JSON serialization)
- Frontend (metadata contract)

---

## 12. Dependency on Other Migrations

| Depends On | Reason |
|---|---|
| M001 (Phase 1 — Dictionary foundation) | FKs to sys_table, sys_column, sys_process |
| M002 (Phase 2 — if any) | Must run in order |

M001 creates: sys_element, sys_reference, sys_reference_list, sys_reference_table, sys_val_rule, sys_table, sys_column (Phase 1 expanded the HLD schema).

M003 FKs reference sys_table (from M001), sys_column (from M001), sys_process (from M001), and the new UI tables (created within M003).

**FK ordering within M003:**
1. sys_window (no FKs to other new tables except default_tab_id)
2. sys_field_group (no FKs to other new tables)
3. sys_tab (FK to sys_window)
4. sys_field (FK to sys_tab, sys_column, sys_field_group)
5. sys_menu (FK to sys_menu self-reference, sys_window, sys_process)

Wait — sys_window has a FK to sys_tab (default_tab_id). This creates a circular dependency. **Resolution:** Remove the FK from sys_window.default_tab_id. Instead, validate the default tab relationship in application code (ensure the tab belongs to this window). This avoids circular FK while maintaining referential integrity at the application level.

**Updated sys_window:** Remove `default_tab_id` FK. The field remains but without FK constraint.
