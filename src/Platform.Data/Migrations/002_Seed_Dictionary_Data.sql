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
-- Explicit INSERTs for SysTable columns (the most critical seed).
-- This avoids CTE complexity that can silently fail.

-- Helper: get IDs of seeded tables
-- Using subqueries in VALUES to keep it simple and explicit.

-- Seed SysColumn for SysTable (the metadata table definition)
INSERT INTO "SysColumn" ("SysTable_ID", "ColumnName", "SysElement_ID", "SysReference_ID",
    "FieldLength", "IsMandatory", "IsKey", "IsUpdateable", "SeqNo", "IsActive")
VALUES
    ((SELECT "SysTable_ID" FROM "SysTable" WHERE "TableName" = 'SysTable'),
     'SysTable_ID',
     (SELECT "SysElement_ID" FROM "SysElement" WHERE "ColumnName" = 'SysTable_ID'),
     (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Integer'),
     NULL, TRUE, TRUE, TRUE, 1, TRUE),
    ((SELECT "SysTable_ID" FROM "SysTable" WHERE "TableName" = 'SysTable'),
     'TableName',
     (SELECT "SysElement_ID" FROM "SysElement" WHERE "ColumnName" = 'TableName'),
     (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Text'),
     60, TRUE, FALSE, TRUE, 2, TRUE),
    ((SELECT "SysTable_ID" FROM "SysTable" WHERE "TableName" = 'SysTable'),
     'ClassName',
     (SELECT "SysElement_ID" FROM "SysElement" WHERE "ColumnName" = 'ClassName'),
     (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Text'),
     120, FALSE, FALSE, TRUE, 3, TRUE),
    ((SELECT "SysTable_ID" FROM "SysTable" WHERE "TableName" = 'SysTable'),
     'Description',
     (SELECT "SysElement_ID" FROM "SysElement" WHERE "ColumnName" = 'Description'),
     (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Text'),
     255, FALSE, FALSE, TRUE, 4, TRUE),
    ((SELECT "SysTable_ID" FROM "SysTable" WHERE "TableName" = 'SysTable'),
     'IsView',
     (SELECT "SysElement_ID" FROM "SysElement" WHERE "ColumnName" = 'IsView'),
     (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'YesNo'),
     1, FALSE, FALSE, TRUE, 5, TRUE),
    ((SELECT "SysTable_ID" FROM "SysTable" WHERE "TableName" = 'SysTable'),
     'AccessLevel',
     (SELECT "SysElement_ID" FROM "SysElement" WHERE "ColumnName" = 'AccessLevel'),
     (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Integer'),
     NULL, FALSE, FALSE, TRUE, 6, TRUE),
    ((SELECT "SysTable_ID" FROM "SysTable" WHERE "TableName" = 'SysTable'),
     'IsChangeLog',
     (SELECT "SysElement_ID" FROM "SysElement" WHERE "ColumnName" = 'IsChangeLog'),
     (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'YesNo'),
     1, FALSE, FALSE, TRUE, 7, TRUE),
    ((SELECT "SysTable_ID" FROM "SysTable" WHERE "TableName" = 'SysTable'),
     'IsDeleteable',
     (SELECT "SysElement_ID" FROM "SysElement" WHERE "ColumnName" = 'IsDeleteable'),
     (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'YesNo'),
     1, FALSE, FALSE, TRUE, 8, TRUE),
    ((SELECT "SysTable_ID" FROM "SysTable" WHERE "TableName" = 'SysTable'),
     'IsHighVolume',
     (SELECT "SysElement_ID" FROM "SysElement" WHERE "ColumnName" = 'IsHighVolume'),
     (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'YesNo'),
     1, FALSE, FALSE, TRUE, 9, TRUE),
    ((SELECT "SysTable_ID" FROM "SysTable" WHERE "TableName" = 'SysTable'),
     'EntityType',
     (SELECT "SysElement_ID" FROM "SysElement" WHERE "ColumnName" = 'EntityType'),
     (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'Text'),
     10, FALSE, FALSE, TRUE, 10, TRUE),
    ((SELECT "SysTable_ID" FROM "SysTable" WHERE "TableName" = 'SysTable'),
     'IsActive',
     (SELECT "SysElement_ID" FROM "SysElement" WHERE "ColumnName" = 'IsActive'),
     (SELECT "SysReference_ID" FROM "SysReference" WHERE "Name" = 'YesNo'),
     1, FALSE, FALSE, TRUE, 11, TRUE)
ON CONFLICT DO NOTHING;
