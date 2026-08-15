-- Phase 3 — UI Metadata Tables
-- Migration 003: Create UI metadata tables for metadata-driven forms, grids, lookups, and menus
-- Naming convention: Pascal_case (matches Phase 1 convention)

-- ------------------------------------------------------------------
-- SysWindow — full-screen window definitions
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysWindow" (
    "SysWindow_ID"        SERIAL          PRIMARY KEY,
    "ColumnName"          VARCHAR(60)     NOT NULL UNIQUE,
    "Name"                VARCHAR(120)    NOT NULL,
    "Description"         VARCHAR(255),
    "Help"                TEXT,
    "DefaultTab_ID"       INT,
    "AccessLevel"         SMALLINT        NOT NULL DEFAULT 3,
    "IsView"              BOOLEAN         NOT NULL DEFAULT FALSE,
    "EntityType"          VARCHAR(20)     NOT NULL DEFAULT 'D',
    "IsActive"            BOOLEAN         NOT NULL DEFAULT TRUE,
    "CreatedBy"           INT,
    "CreatedAt"           TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    "UpdatedBy"           INT,
    "UpdatedAt"           TIMESTAMPTZ
);

COMMENT ON TABLE "SysWindow" IS 'Top-level window definitions (e.g., Library Book, User Management).';

-- No FK on DefaultTab_ID — circular with SysTab. Validated in application code.
CREATE UNIQUE INDEX IF NOT EXISTS uq_sys_window_column_name ON "SysWindow" ("ColumnName");
CREATE INDEX IF NOT EXISTS ix_sys_window_is_active ON "SysWindow" ("IsActive") WHERE "IsActive" = TRUE;

-- ------------------------------------------------------------------
-- SysTab — tabs within windows, bound to data tables
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysTab" (
    "SysTab_ID"           SERIAL          PRIMARY KEY,
    "SysWindow_ID"        INT             NOT NULL REFERENCES "SysWindow" ("SysWindow_ID") ON DELETE CASCADE,
    "SysTable_ID"         INT             NOT NULL REFERENCES "SysTable" ("SysTable_ID"),
    "ColumnName"          VARCHAR(60)     NOT NULL,
    "Name"                VARCHAR(120)    NOT NULL,
    "SeqNo"               INT             NOT NULL DEFAULT 0,
    "IsDefaultTab"        BOOLEAN         NOT NULL DEFAULT FALSE,
    "IsGrid"              BOOLEAN         NOT NULL DEFAULT FALSE,
    "WhereClause"         VARCHAR(500),
    "IsDeleteable"        BOOLEAN         NOT NULL DEFAULT TRUE,
    "EntityType"          VARCHAR(20)     NOT NULL DEFAULT 'D',
    "IsActive"            BOOLEAN         NOT NULL DEFAULT TRUE,
    "CreatedBy"           INT,
    "CreatedAt"           TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    "UpdatedBy"           INT,
    "UpdatedAt"           TIMESTAMPTZ,
    UNIQUE ("SysWindow_ID", "ColumnName")
);

COMMENT ON TABLE "SysTab" IS 'Tabs within windows, each bound to a data table (SysTable).';

CREATE INDEX IF NOT EXISTS ix_sys_tab_window ON "SysTab" ("SysWindow_ID");
CREATE INDEX IF NOT EXISTS ix_sys_tab_table ON "SysTab" ("SysTable_ID");
CREATE INDEX IF NOT EXISTS ix_sys_tab_is_active ON "SysTab" ("IsActive") WHERE "IsActive" = TRUE;
CREATE INDEX IF NOT EXISTS ix_sys_tab_seq_no ON "SysTab" ("SysWindow_ID", "SeqNo");

-- ------------------------------------------------------------------
-- SysFieldGroup — collapsible field sections within tabs
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysFieldGroup" (
    "SysFieldGroup_ID"    SERIAL          PRIMARY KEY,
    "SysTab_ID"           INT             NOT NULL REFERENCES "SysTab" ("SysTab_ID") ON DELETE CASCADE,
    "ColumnName"          VARCHAR(60)     NOT NULL,
    "Name"                VARCHAR(120)    NOT NULL,
    "SeqNo"               INT             NOT NULL DEFAULT 0,
    "ColSpan"             INT             NOT NULL DEFAULT 12,
    "IsCollapsed"         BOOLEAN         NOT NULL DEFAULT FALSE,
    "EntityType"          VARCHAR(20)     NOT NULL DEFAULT 'D',
    "IsActive"            BOOLEAN         NOT NULL DEFAULT TRUE,
    "CreatedBy"           INT,
    "CreatedAt"           TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    "UpdatedBy"           INT,
    "UpdatedAt"           TIMESTAMPTZ,
    UNIQUE ("SysTab_ID", "ColumnName")
);

COMMENT ON TABLE "SysFieldGroup" IS 'Collapsible field sections within a tab.';

CREATE INDEX IF NOT EXISTS ix_sys_field_group_tab ON "SysFieldGroup" ("SysTab_ID");
CREATE INDEX IF NOT EXISTS ix_sys_field_group_is_active ON "SysFieldGroup" ("IsActive") WHERE "IsActive" = TRUE;
CREATE INDEX IF NOT EXISTS ix_sys_field_group_seq_no ON "SysFieldGroup" ("SysTab_ID", "SeqNo");

-- ------------------------------------------------------------------
-- SysField — field rendering within tabs, mapped to data columns
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysField" (
    "SysField_ID"             SERIAL          PRIMARY KEY,
    "SysTab_ID"               INT             NOT NULL REFERENCES "SysTab" ("SysTab_ID") ON DELETE CASCADE,
    "SysColumn_ID"            INT             NOT NULL REFERENCES "SysColumn" ("SysColumn_ID"),
    "ColumnName"              VARCHAR(60)     NOT NULL,
    "Name"                    VARCHAR(120)    NOT NULL,
    "ControlType"             VARCHAR(30)     NOT NULL,
    "SysFieldGroup_ID"        INT             REFERENCES "SysFieldGroup" ("SysFieldGroup_ID"),
    "SeqNo"                   INT             NOT NULL DEFAULT 0,
    "IsMandatoryOverride"     BOOLEAN         NOT NULL DEFAULT FALSE,
    "IsReadOnlyOverride"      BOOLEAN         NOT NULL DEFAULT FALSE,
    "ColSpan"                 INT             NOT NULL DEFAULT 1,
    "RowSpan"                 INT             NOT NULL DEFAULT 1,
    "DisplayLogic"            VARCHAR(500),
    "ReadOnlyLogic"           VARCHAR(500),
    "MandatoryLogic"          VARCHAR(500),
    "DefaultValue"            VARCHAR(255),
    "EntityType"              VARCHAR(20)     NOT NULL DEFAULT 'D',
    "IsActive"                BOOLEAN         NOT NULL DEFAULT TRUE,
    "CreatedBy"               INT,
    "CreatedAt"               TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    "UpdatedBy"               INT,
    "UpdatedAt"               TIMESTAMPTZ,
    UNIQUE ("SysTab_ID", "SysColumn_ID")
);

COMMENT ON TABLE "SysField" IS 'Field definitions within tabs, mapped to data columns.';

CREATE INDEX IF NOT EXISTS ix_sys_field_tab ON "SysField" ("SysTab_ID");
CREATE INDEX IF NOT EXISTS ix_sys_field_column ON "SysField" ("SysColumn_ID");
CREATE INDEX IF NOT EXISTS ix_sys_field_group ON "SysField" ("SysFieldGroup_ID");
CREATE INDEX IF NOT EXISTS ix_sys_field_is_active ON "SysField" ("IsActive") WHERE "IsActive" = TRUE;
CREATE INDEX IF NOT EXISTS ix_sys_field_seq_no ON "SysField" ("SysTab_ID", "SeqNo");

-- ------------------------------------------------------------------
-- SysMenu — navigation menu hierarchy
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysMenu" (
    "SysMenu_ID"        SERIAL          PRIMARY KEY,
    "Parent_ID"         INT             REFERENCES "SysMenu" ("SysMenu_ID") ON DELETE CASCADE,
    "ColumnName"        VARCHAR(60)     NOT NULL UNIQUE,
    "Name"              VARCHAR(120)    NOT NULL,
    "Icon"              VARCHAR(60),
    "Sequence"          INT             NOT NULL DEFAULT 0,
    "Window_ID"         INT             REFERENCES "SysWindow" ("SysWindow_ID"),
    "Process_ID"        INT             REFERENCES "SysProcess" ("SysProcess_ID"),
    "IsSeparator"       BOOLEAN         NOT NULL DEFAULT FALSE,
    "IsSystem"          BOOLEAN         NOT NULL DEFAULT FALSE,
    "EntityType"        VARCHAR(20)     NOT NULL DEFAULT 'D',
    "IsActive"          BOOLEAN         NOT NULL DEFAULT TRUE,
    "CreatedBy"         INT,
    "CreatedAt"         TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    "UpdatedBy"         INT,
    "UpdatedAt"         TIMESTAMPTZ
);

COMMENT ON TABLE "SysMenu" IS 'Hierarchical navigation menu (self-referencing).';

CREATE INDEX IF NOT EXISTS ix_sys_menu_parent ON "SysMenu" ("Parent_ID");
CREATE INDEX IF NOT EXISTS ix_sys_menu_is_active ON "SysMenu" ("IsActive") WHERE "IsActive" = TRUE;
CREATE INDEX IF NOT EXISTS ix_sys_menu_sequence ON "SysMenu" ("Sequence");
CREATE INDEX IF NOT EXISTS ix_sys_menu_window ON "SysMenu" ("Window_ID");

-- ------------------------------------------------------------------
-- Seed data (idempotent)
-- ------------------------------------------------------------------
-- Sample window for testing (Library Book example from HLD/LLD)
INSERT INTO "SysWindow" ("ColumnName", "Name", "Description", "AccessLevel", "EntityType")
VALUES ('window_library_book', 'Library Book', 'Manage library book records', 3, 'D')
ON CONFLICT ("ColumnName") DO NOTHING;

-- Sample tab for the Library Book window
DO $$
DECLARE
    v_window_id INT;
    v_table_id  INT;
BEGIN
    SELECT "SysWindow_ID" INTO v_window_id FROM "SysWindow" WHERE "ColumnName" = 'window_library_book';
    IF v_window_id IS NOT NULL THEN
        SELECT "SysTable_ID" INTO v_table_id FROM "SysTable" WHERE "TableName" = 'library_book';
        IF v_table_id IS NOT NULL THEN
            INSERT INTO "SysTab" ("SysWindow_ID", "SysTable_ID", "ColumnName", "Name", "SeqNo", "IsDefaultTab", "EntityType")
            VALUES (v_window_id, v_table_id, 'main', 'Main', 0, TRUE, 'D')
            ON CONFLICT ("SysWindow_ID", "ColumnName") DO NOTHING;
        END IF;
    END IF;
END $$;
