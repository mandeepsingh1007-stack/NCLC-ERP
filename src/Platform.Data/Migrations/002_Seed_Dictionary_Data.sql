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
    ('MaxLength', 'Maximum string length validation', 'VARCHAR', 'MAXLENGTH')
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
