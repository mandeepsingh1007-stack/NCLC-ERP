-- Phase 1 — Dictionary Seed Data
-- Migration 002: Seed initial reference types, validation rules, and table/column definitions

-- ------------------------------------------------------------------
-- Seed SysReference types (validation types)
-- ------------------------------------------------------------------
INSERT INTO "SysReference" ("Name", "ValidationType", "IsSystemType") VALUES
    ('String', 'LIST', TRUE),
    ('Integer', 'LIST', TRUE),
    ('Decimal', 'LIST', TRUE),
    ('Date', 'LIST', TRUE),
    ('DateTime', 'LIST', TRUE),
    ('YesNo', 'LIST', TRUE),
    ('List', 'LIST', TRUE),
    ('Table', 'TABLE', TRUE),
    ('Search', 'SEARCH', TRUE),
    ('Text', 'LIST', TRUE),
    ('Binary', 'LIST', TRUE)
ON CONFLICT ("Name") DO NOTHING;

-- ------------------------------------------------------------------
-- Seed SysValRule
-- ------------------------------------------------------------------
INSERT INTO "SysValRule" ("Name", "Description", "RuleType", "Code") VALUES
    ('NotNull', 'Value must not be null or empty', 'SQL', 'VALUE IS NOT NULL'),
    ('MaxLength', 'Maximum string length validation', 'SQL', 'VALUE IS NOT NULL')
ON CONFLICT ("Name") DO NOTHING;

-- ------------------------------------------------------------------
-- Seed SysTable definitions
-- ------------------------------------------------------------------
INSERT INTO "SysTable" ("TableName", "ClassName", "Description", "IsView", "AccessLevel", "EntityType") VALUES
    ('SysElement', 'SysElement', 'Base dictionary entity for translatable items', FALSE, 0, 'D'),
    ('SysElement_Trl', 'SysElementTranslation', 'Translations for SysElement', FALSE, 0, 'D'),
    ('SysReference', 'SysReference', 'Reference types for column validation', FALSE, 0, 'D'),
    ('SysReferenceList', 'SysReferenceList', 'List values within a reference', FALSE, 0, 'D'),
    ('SysValRule', 'SysValRule', 'Validation rules attachable to columns', FALSE, 0, 'D'),
    ('SysTable', 'SysTable', 'Dictionary table definitions', FALSE, 0, 'D'),
    ('SysColumn', 'SysColumn', 'Column definitions within a table', FALSE, 0, 'D')
ON CONFLICT ("TableName") DO NOTHING;

-- ------------------------------------------------------------------
-- Seed SysReferenceTable — map reference types to tables
-- ------------------------------------------------------------------
-- Table references: each reference type links to tables that use it
INSERT INTO "SysReferenceTable" ("SysReference_ID", "SysTable_ID", "KeyColumn", "DisplayColumn") VALUES
    ((SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'List'),
     (SELECT "SysTable_ID" FROM "SysTable" WHERE "TableName" = 'SysReferenceList'),
     'Value', 'Name')
ON CONFLICT ("SysReference_ID") DO NOTHING;

-- ------------------------------------------------------------------
-- Seed SysElement entries for all seed table column names
-- ------------------------------------------------------------------
INSERT INTO "SysElement" ("ColumnName", "Name", "Description", "IsActive") VALUES
    -- SysTable columns
    ('SysTable_ID', 'Table ID', 'Unique identifier', TRUE),
    ('TableName', 'Table Name', 'Logical table name', TRUE),
    ('ClassName', 'Class Name', 'Generated class name', TRUE),
    ('Description', 'Description', 'Human-readable description', TRUE),
    ('IsView', 'Is View', 'True if table is a view', TRUE),
    ('AccessLevel', 'Access Level', 'Access level code', TRUE),
    ('IsChangeLog', 'Change Log', 'Track changes', TRUE),
    ('IsDeleteable', 'Deleteable', 'Allow deletion', TRUE),
    ('IsHighVolume', 'High Volume', 'High volume indicator', TRUE),
    ('EntityType', 'Entity Type', 'D=Data, M=Master', TRUE),
    ('IsActive', 'Active', 'Active flag', TRUE),

    -- SysColumn columns
    ('SysColumn_ID', 'Column ID', 'Unique identifier', TRUE),
    ('SysTable_ID', 'Table ID', 'Parent table reference', TRUE),
    ('ColumnName', 'Column Name', 'Column name', TRUE),
    ('SysElement_ID', 'Element ID', 'Display element reference', TRUE),
    ('SysReference_ID', 'Reference ID', 'Validation reference', TRUE),
    ('FieldLength', 'Field Length', 'Max field length', TRUE),
    ('IsMandatory', 'Mandatory', 'Required field', TRUE),
    ('IsKey', 'Key', 'Key field', TRUE),
    ('IsUpdateable', 'Updateable', 'Allow updates', TRUE),
    ('DefaultValue', 'Default Value', 'Default value expression', TRUE),
    ('SeqNo', 'Sequence', 'Display order', TRUE),
    ('IsActive', 'Active', 'Active flag', TRUE),

    -- SysReference columns
    ('SysReference_ID', 'Reference ID', 'Unique identifier', TRUE),
    ('Name', 'Reference Name', 'Logical name', TRUE),
    ('ValidationType', 'Validation Type', 'LIST/TABLE/SEARCH', TRUE),
    ('IsSystemType', 'System Type', 'Built-in reference', TRUE),

    -- SysValRule columns
    ('SysValRule_ID', 'Rule ID', 'Unique identifier', TRUE),
    ('Name', 'Rule Name', 'Rule name', TRUE),
    ('Description', 'Description', 'Rule description', TRUE),
    ('RuleType', 'Rule Type', 'SQL/REGEX/LAMBDA/SCRIPT', TRUE),
    ('Code', 'Rule Code', 'Rule expression/code', TRUE),
    ('IsActive', 'Active', 'Active flag', TRUE)
ON CONFLICT ("ColumnName") DO NOTHING;

-- ------------------------------------------------------------------
-- Seed SysColumn — column definitions for each seeded SysTable
-- ------------------------------------------------------------------
-- We seed SysColumn rows for each seeded SysTable using explicit
-- mapping. Each row links: SysTable_ID → ColumnName → SysElement_ID
-- → optional SysReference_ID for type/validation hints.

-- Helper: verify all dependencies exist
DO $body$
BEGIN
    -- Ensure SysElement, SysReference, SysTable are populated
    IF (SELECT COUNT(*) FROM "SysElement") = 0 THEN
        RAISE EXCEPTION 'SysElement is empty — must seed SysElement before SysColumn';
    END IF;
    IF (SELECT COUNT(*) FROM "SysReference") = 0 THEN
        RAISE EXCEPTION 'SysReference is empty — must seed SysReference before SysColumn';
    END IF;
    IF (SELECT COUNT(*) FROM "SysTable") = 0 THEN
        RAISE EXCEPTION 'SysTable is empty — must seed SysTable before SysColumn';
    END IF;
END $body$;

-- Seed SysColumn for each seeded SysTable.
-- Strategy: use a CTE that generates all (SysTable, ColumnName) pairs,
-- then join SysElement to get display names.

WITH table_columns AS (
    -- SysTable columns
    SELECT 'SysTable'::TEXT AS tname, col::TEXT AS cname FROM unnest(ARRAY[
        'SysTable_ID','TableName','ClassName','Description',
        'IsView','AccessLevel','IsChangeLog','IsDeleteable',
        'IsHighVolume','EntityType','IsActive'
    ]) col
    UNION ALL
    -- SysColumn columns
    SELECT 'SysColumn', col FROM unnest(ARRAY[
        'SysColumn_ID','SysTable_ID','ColumnName','SysElement_ID',
        'SysReference_ID','SysValRule_ID','SysReferenceValue_ID',
        'FieldLength','IsMandatory','IsKey','IsParent','IsIdentifier',
        'IsSelectionColumn','IsEncrypted','IsUpdateable','IsAlwaysUpdateable',
        'DefaultValue','ValueMin','ValueMax','SeqNo','EntityType','IsActive'
    ]) col
    UNION ALL
    -- SysReference columns
    SELECT 'SysReference', col FROM unnest(ARRAY[
        'SysReference_ID','Name','ValidationType',
        'IsSystemType','ValueFormat','IsActive'
    ]) col
    UNION ALL
    -- SysValRule columns
    SELECT 'SysValRule', col FROM unnest(ARRAY[
        'SysValRule_ID','Name','Description','RuleType','Code','IsActive'
    ]) col
    UNION ALL
    -- SysElement columns
    SELECT 'SysElement', col FROM unnest(ARRAY[
        'SysElement_ID','ColumnName','Name','Description',
        'IsActive','Help','Tooltip','DefaultFormat','SysWindow_ID'
    ]) col
    UNION ALL
    -- SysReferenceList columns
    SELECT 'SysReferenceList', col FROM unnest(ARRAY[
        'SysReferenceList_ID','SysReference_ID','Value','Name',
        'Description','SeqNo','IsActive'
    ]) col
    UNION ALL
    -- SysReferenceTable columns
    SELECT 'SysReferenceTable', col FROM unnest(ARRAY[
        'SysReferenceTable_ID','SysReference_ID','SysTable_ID',
        'KeyColumn','DisplayColumn','IsActive'
    ]) col
),
mapped AS (
    SELECT
        st."SysTable_ID",
        tc.cname AS "ColumnName",
        se."SysElement_ID",
        CASE tc.cname
            WHEN 'SysTable_ID' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Integer')
            WHEN 'AccessLevel' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Integer')
            WHEN 'SeqNo' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Integer')
            WHEN 'FieldLength' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Integer')
            WHEN 'SysColumn_ID' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Integer')
            WHEN 'SysReference_ID' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Integer')
            WHEN 'SysValRule_ID' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Integer')
            WHEN 'SysElement_ID' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Integer')
            WHEN 'SysReferenceList_ID' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Integer')
            WHEN 'SysReferenceTable_ID' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Integer')
            WHEN 'SysReferenceValue_ID' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Integer')
            WHEN 'SysWindow_ID' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Integer')
            WHEN 'IsView' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'YesNo')
            WHEN 'IsChangeLog' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'YesNo')
            WHEN 'IsDeleteable' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'YesNo')
            WHEN 'IsHighVolume' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'YesNo')
            WHEN 'IsActive' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'YesNo')
            WHEN 'IsKey' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'YesNo')
            WHEN 'IsMandatory' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'YesNo')
            WHEN 'IsUpdateable' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'YesNo')
            WHEN 'IsParent' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'YesNo')
            WHEN 'IsIdentifier' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'YesNo')
            WHEN 'IsSelectionColumn' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'YesNo')
            WHEN 'IsEncrypted' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'YesNo')
            WHEN 'IsAlwaysUpdateable' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'YesNo')
            WHEN 'ValidationType' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Text')
            WHEN 'RuleType' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Text')
            WHEN 'EntityType' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Text')
            WHEN 'ValueFormat' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Text')
            WHEN 'DefaultFormat' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Text')
            WHEN 'FormatString' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Text')
            WHEN 'ValueMin' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Text')
            WHEN 'ValueMax' THEN (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Text')
            ELSE (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Text')
        END,
        CASE tc.cname
            WHEN 'ClassName' THEN 120
            WHEN 'Description' THEN 255
            WHEN 'TableName' THEN 60
            WHEN 'EntityType' THEN 10
            WHEN 'ValueFormat' THEN 60
            WHEN 'Name' THEN 60
            WHEN 'RuleType' THEN 10
            WHEN 'Code' THEN 500
            WHEN 'Help' THEN 500
            WHEN 'DefaultFormat' THEN 100
            WHEN 'Tooltip' THEN 255
            WHEN 'Placeholder' THEN 100
            WHEN 'FormatString' THEN 50
            WHEN 'ValidationType' THEN 10
            WHEN 'IsView' THEN 1
            WHEN 'IsChangeLog' THEN 1
            WHEN 'IsDeleteable' THEN 1
            WHEN 'IsHighVolume' THEN 1
            WHEN 'IsActive' THEN 1
            WHEN 'IsKey' THEN 1
            WHEN 'IsMandatory' THEN 1
            WHEN 'IsUpdateable' THEN 1
            WHEN 'IsParent' THEN 1
            WHEN 'IsIdentifier' THEN 1
            WHEN 'IsSelectionColumn' THEN 1
            WHEN 'IsEncrypted' THEN 1
            WHEN 'IsAlwaysUpdateable' THEN 1
            ELSE NULL
        END
    FROM "SysTable" st
    JOIN table_columns tc ON tc.tname = st."TableName"
    LEFT JOIN "SysElement" se ON se."ColumnName" = tc.cname
)
INSERT INTO "SysColumn" ("SysTable_ID", "ColumnName", "SysElement_ID", "SysReference_ID",
    "FieldLength", "IsMandatory", "IsKey", "IsUpdateable", "SeqNo", "IsActive")
SELECT
    "SysTable_ID",
    "ColumnName",
    "SysElement_ID",
    "SysReference_ID",
    "FieldLength",
    CASE "ColumnName"
        WHEN 'TableName' THEN TRUE
        WHEN 'ColumnName' THEN TRUE
        ELSE FALSE
    END,
    CASE "ColumnName"
        WHEN 'SysTable_ID' THEN TRUE
        WHEN 'SysColumn_ID' THEN TRUE
        WHEN 'SysReference_ID' THEN TRUE
        WHEN 'SysValRule_ID' THEN TRUE
        WHEN 'SysElement_ID' THEN TRUE
        WHEN 'SysReferenceList_ID' THEN TRUE
        WHEN 'SysReferenceTable_ID' THEN TRUE
        ELSE FALSE
    END,
    TRUE,
    row_number() OVER (PARTITION BY "SysTable_ID" ORDER BY "ColumnName"),
    TRUE
FROM mapped
WHERE "SysElement_ID" IS NOT NULL
ON CONFLICT DO NOTHING;
