-- Phase 1 — Dictionary Foundation Schema
-- Migration 001: Create all dictionary tables
-- Authoritative spec: FINAL-MASTER-HLD-LLD-v2.md Section 7

-- ------------------------------------------------------------------
-- SysElement — base dictionary entity
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysElement" (
    "SysElement_ID"    SERIAL PRIMARY KEY,
    "ColumnName"       VARCHAR(60) NOT NULL UNIQUE,
    "Name"             VARCHAR(120) NOT NULL,
    "Description"      VARCHAR(255),
    "Help"             TEXT,
    "IsActive"         BOOLEAN NOT NULL DEFAULT TRUE
);

COMMENT ON TABLE "SysElement" IS 'Base dictionary entity. Every translatable dictionary item is a SysElement.';

-- ------------------------------------------------------------------
-- SysElement_Trl — translations for SysElement
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysElement_Trl" (
    "SysElement_ID"    INT NOT NULL REFERENCES "SysElement" ("SysElement_ID") ON DELETE CASCADE,
    "Language"         VARCHAR(10) NOT NULL,
    "Name"             VARCHAR(120),
    "Description"      VARCHAR(255),
    "Help"             TEXT,
    PRIMARY KEY ("SysElement_ID", "Language")
);

COMMENT ON TABLE "SysElement_Trl" IS 'Translations for SysElement in a specific language.';

-- ------------------------------------------------------------------
-- SysReference — reference types (list, table, search)
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysReference" (
    "SysReference_ID"   SERIAL PRIMARY KEY,
    "Name"              VARCHAR(60) NOT NULL UNIQUE,
    "ValidationType"    VARCHAR(10) NOT NULL,
    "IsSystemType"      BOOLEAN NOT NULL DEFAULT FALSE,
    "ValueFormat"       VARCHAR(60)
);

COMMENT ON TABLE "SysReference" IS 'Reference types used for column validation (list, table, search).';

-- ------------------------------------------------------------------
-- SysReferenceList — value entries within a list reference
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysReferenceList" (
    "SysReferenceList_ID"  SERIAL PRIMARY KEY,
    "SysReference_ID"      INT NOT NULL REFERENCES "SysReference" ("SysReference_ID") ON DELETE CASCADE,
    "Value"                VARCHAR(30) NOT NULL,
    "Name"                 VARCHAR(60) NOT NULL,
    "SeqNo"                INT NOT NULL DEFAULT 0,
    "IsActive"             BOOLEAN NOT NULL DEFAULT TRUE,
    UNIQUE ("SysReference_ID", "Value")
);

-- ------------------------------------------------------------------
-- SysValRule — validation rules
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysValRule" (
    "SysValRule_ID"   SERIAL PRIMARY KEY,
    "Name"            VARCHAR(120) NOT NULL UNIQUE,
    "Description"     VARCHAR(255),
    "RuleType"        VARCHAR(10) NOT NULL DEFAULT 'SQL',
    "Code"            VARCHAR(2000) NOT NULL,
    "IsActive"        BOOLEAN NOT NULL DEFAULT TRUE
);

COMMENT ON TABLE "SysValRule" IS 'Validation rules attachable to SysColumn.';

-- ------------------------------------------------------------------
-- SysTable — table definitions
-- MUST precede SysReferenceTable and SysColumn (FK dependencies)
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysTable" (
    "SysTable_ID"        SERIAL PRIMARY KEY,
    "TableName"          VARCHAR(60) NOT NULL UNIQUE,
    "ClassName"          VARCHAR(120),
    "Description"        VARCHAR(255),
    "IsView"             BOOLEAN NOT NULL DEFAULT FALSE,
    "AccessLevel"        SMALLINT NOT NULL DEFAULT 3,
    "IsChangeLog"        BOOLEAN NOT NULL DEFAULT FALSE,
    "IsDeleteable"       BOOLEAN NOT NULL DEFAULT TRUE,
    "IsHighVolume"       BOOLEAN NOT NULL DEFAULT FALSE,
    "ReplicationType"    VARCHAR(10) NOT NULL DEFAULT 'L',
    "SysWindow_ID"       INT,
    "EntityType"         VARCHAR(20) NOT NULL DEFAULT 'D',
    "IsActive"           BOOLEAN NOT NULL DEFAULT TRUE
);

COMMENT ON TABLE "SysTable" IS 'Dictionary table definition. Each row represents a business table the platform manages.';

-- ------------------------------------------------------------------
-- SysReferenceTable — table reference definitions
-- FK depends on both SysReference (created above) and SysTable (created above)
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysReferenceTable" (
    "SysReference_ID"    INT PRIMARY KEY REFERENCES "SysReference" ("SysReference_ID") ON DELETE CASCADE,
    "SysTable_ID"        INT NOT NULL REFERENCES "SysTable" ("SysTable_ID"),
    "KeyColumn"          VARCHAR(60) NOT NULL,
    "DisplayColumn"      VARCHAR(60) NOT NULL,
    "WhereClause"        VARCHAR(500),
    "OrderByClause"      VARCHAR(255)
);

-- ------------------------------------------------------------------
-- SysColumn — column definitions within a SysTable
-- FK depends on SysTable, SysElement, SysReference, SysValRule
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysColumn" (
    "SysColumn_ID"          SERIAL PRIMARY KEY,
    "SysTable_ID"           INT NOT NULL REFERENCES "SysTable" ("SysTable_ID") ON DELETE CASCADE,
    "ColumnName"            VARCHAR(60) NOT NULL,
    "SysElement_ID"         INT REFERENCES "SysElement" ("SysElement_ID"),
    "SysReference_ID"       INT NOT NULL REFERENCES "SysReference" ("SysReference_ID"),
    "SysReferenceValue_ID"  INT,
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
    "DefaultValue"          VARCHAR(255),
    "ValueMin"              VARCHAR(60),
    "ValueMax"              VARCHAR(60),
    "SeqNo"                 INT NOT NULL DEFAULT 0,
    "EntityType"            VARCHAR(20) NOT NULL DEFAULT 'D',
    "IsActive"              BOOLEAN NOT NULL DEFAULT TRUE,
    UNIQUE ("SysTable_ID", "ColumnName")
);

COMMENT ON TABLE "SysColumn" IS 'Column definition within a SysTable. Links to SysElement, SysReference, and SysValRule.';
