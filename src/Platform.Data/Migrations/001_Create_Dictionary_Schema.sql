-- Phase 1 — Dictionary Foundation Schema
-- Migration 001: Create all dictionary tables

-- ------------------------------------------------------------------
-- SysElement — base dictionary entity
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysElement" (
    "SysElement_ID"    SERIAL PRIMARY KEY,
    "ColumnName"       VARCHAR(100) NOT NULL UNIQUE,
    "Name"             VARCHAR(200) NOT NULL,
    "Description"      TEXT,
    "Help"             TEXT,
    "IsActive"         BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedBy"        INT DEFAULT 0,
    "CreatedDate"      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedBy"        INT DEFAULT 0,
    "UpdatedDate"      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_SysElement_IsActive" ON "SysElement" ("IsActive");

COMMENT ON TABLE "SysElement" IS 'Base dictionary entity. Every translatable dictionary item is a SysElement.';

-- ------------------------------------------------------------------
-- SysElement_Trl — translations for SysElement
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysElement_Trl" (
    "SysElement_ID"    INT NOT NULL REFERENCES "SysElement" ("SysElement_ID") ON DELETE CASCADE,
    "Language"         CHAR(5) NOT NULL,
    "Name"             VARCHAR(200),
    "Description"      TEXT,
    "Help"             TEXT,
    PRIMARY KEY ("SysElement_ID", "Language")
);

CREATE INDEX IF NOT EXISTS "IX_SysElement_Trl_Language" ON "SysElement_Trl" ("Language");

COMMENT ON TABLE "SysElement_Trl" IS 'Translations for SysElement in a specific language.';

-- ------------------------------------------------------------------
-- SysReference — reference types (list, table, search)
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysReference" (
    "SysReference_ID"   SERIAL PRIMARY KEY,
    "Name"              VARCHAR(100) NOT NULL UNIQUE,
    "ValidationType"    VARCHAR(10) NOT NULL,
    "IsSystemType"      BOOLEAN NOT NULL DEFAULT FALSE,
    "ValueFormat"       VARCHAR(100),
    "CreatedBy"         INT DEFAULT 0,
    "CreatedDate"       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedBy"         INT DEFAULT 0,
    "UpdatedDate"       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_SysReference_Name" ON "SysReference" ("Name");

COMMENT ON TABLE "SysReference" IS 'Reference types used for column validation (list, table, search).';

-- ------------------------------------------------------------------
-- SysReferenceList — value entries within a list reference
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysReferenceList" (
    "SysReferenceList_ID"  SERIAL PRIMARY KEY,
    "SysReference_ID"      INT NOT NULL REFERENCES "SysReference" ("SysReference_ID") ON DELETE CASCADE,
    "Value"                VARCHAR(500) NOT NULL,
    "Name"                 VARCHAR(200) NOT NULL,
    "SeqNo"                INT NOT NULL DEFAULT 0,
    "IsActive"             BOOLEAN NOT NULL DEFAULT TRUE,
    UNIQUE ("SysReference_ID", "Value")
);

CREATE INDEX IF NOT EXISTS "IX_SysReferenceList_SysReference" ON "SysReferenceList" ("SysReference_ID");

-- ------------------------------------------------------------------
-- SysValRule — validation rules
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysValRule" (
    "SysValRule_ID"   SERIAL PRIMARY KEY,
    "Name"            VARCHAR(100) NOT NULL UNIQUE,
    "Description"     TEXT,
    "RuleType"        VARCHAR(10) NOT NULL,
    "Code"            VARCHAR(2000) NOT NULL,
    "IsActive"        BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedBy"       INT DEFAULT 0,
    "CreatedDate"     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedBy"       INT DEFAULT 0,
    "UpdatedDate"     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_SysValRule_Name" ON "SysValRule" ("Name");

COMMENT ON TABLE "SysValRule" IS 'Validation rules attachable to SysColumn.';

-- ------------------------------------------------------------------
-- SysTable — table definitions
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysTable" (
    "SysTable_ID"        SERIAL PRIMARY KEY,
    "TableName"          VARCHAR(100) NOT NULL UNIQUE,
    "ClassName"          VARCHAR(100),
    "Description"        TEXT,
    "IsView"             BOOLEAN NOT NULL DEFAULT FALSE,
    "AccessLevel"        SMALLINT NOT NULL DEFAULT 0,
    "IsChangeLog"        BOOLEAN NOT NULL DEFAULT FALSE,
    "IsDeleteable"       BOOLEAN NOT NULL DEFAULT TRUE,
    "IsHighVolume"       BOOLEAN NOT NULL DEFAULT FALSE,
    "ReplicationType"    VARCHAR(50),
    "SysWindow_ID"       INT,
    "EntityType"         VARCHAR(10) NOT NULL DEFAULT 'D',
    "IsActive"           BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedBy"          INT DEFAULT 0,
    "CreatedDate"        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedBy"          INT DEFAULT 0,
    "UpdatedDate"        TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_SysTable_IsActive" ON "SysTable" ("IsActive");
CREATE INDEX IF NOT EXISTS "IX_SysTable_EntityType" ON "SysTable" ("EntityType");

COMMENT ON TABLE "SysTable" IS 'Dictionary table definition. Each row represents a business table the platform manages.';

-- ------------------------------------------------------------------
-- SysColumn — column definitions within a SysTable
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysColumn" (
    "SysColumn_ID"          SERIAL PRIMARY KEY,
    "SysTable_ID"           INT NOT NULL REFERENCES "SysTable" ("SysTable_ID") ON DELETE CASCADE,
    "ColumnName"            VARCHAR(100) NOT NULL,
    "SysElement_ID"         INT REFERENCES "SysElement" ("SysElement_ID"),
    "SysReference_ID"       INT REFERENCES "SysReference" ("SysReference_ID"),
    "SysReferenceValue_ID"  INT REFERENCES "SysReferenceList" ("SysReferenceList_ID"),
    "SysValRule_ID"         INT REFERENCES "SysValRule" ("SysValRule_ID"),
    "FieldLength"           INT,
    "IsMandatory"           BOOLEAN NOT NULL DEFAULT FALSE,
    "IsKey"                 BOOLEAN NOT NULL DEFAULT FALSE,
    "IsParent"              BOOLEAN NOT NULL DEFAULT FALSE,
    "IsIdentifier"          BOOLEAN NOT NULL DEFAULT FALSE,
    "IsSelectionColumn"     BOOLEAN NOT NULL DEFAULT FALSE,
    "IsEncrypted"           BOOLEAN NOT NULL DEFAULT FALSE,
    "IsUpdateable"          BOOLEAN NOT NULL DEFAULT TRUE,
    "IsAlwaysUpdateable"    BOOLEAN NOT NULL DEFAULT FALSE,
    "DefaultValue"          TEXT,
    "ValueMin"              TEXT,
    "ValueMax"              TEXT,
    "SeqNo"                 INT NOT NULL DEFAULT 0,
    "EntityType"            VARCHAR(10) NOT NULL DEFAULT 'D',
    "IsActive"              BOOLEAN NOT NULL DEFAULT TRUE,
    UNIQUE ("SysTable_ID", "ColumnName")
);

CREATE INDEX IF NOT EXISTS "IX_SysColumn_SysTable" ON "SysColumn" ("SysTable_ID");
CREATE INDEX IF NOT EXISTS "IX_SysColumn_SysReference" ON "SysColumn" ("SysReference_ID");
CREATE INDEX IF NOT EXISTS "IX_SysColumn_SysValRule" ON "SysColumn" ("SysValRule_ID");
CREATE INDEX IF NOT EXISTS "IX_SysColumn_IsActive" ON "SysColumn" ("IsActive");

COMMENT ON TABLE "SysColumn" IS 'Column definition within a SysTable. Links to SysElement, SysReference, and SysValRule.';
