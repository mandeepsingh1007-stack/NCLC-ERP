using System.Collections.Concurrent;
using Dapper;
using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// In-memory representation of all dictionary metadata.
/// Loads all tables, columns, references, and ValRules from the database at startup.
/// Thread-safe for reads (ConcurrentDictionary). Thread-safe for event raising.
/// </summary>
public class MetadataGraph : IMetadataGraph, IDisposable
{
    private readonly string _connectionString;
    private readonly ConcurrentDictionary<string, TableMetadata> _tables = new();
    private readonly ConcurrentDictionary<string, List<MetaColumn>> _tableColumns = new();
    private readonly ConcurrentDictionary<string, List<SysReference>> _references = new();
    private readonly ConcurrentDictionary<string, List<SysReferenceList>> _referenceValues = new();
    private readonly ConcurrentDictionary<int, SysValRule> _valRulesById = new();
    private readonly ConcurrentDictionary<string, string> _refBaseTypes = new(); // refId -> base type name

    // UI metadata
    private readonly ConcurrentDictionary<string, SysWindow> _windows = new();
    private readonly ConcurrentDictionary<int, List<SysTab>> _tabsByWindow = new();
    private readonly ConcurrentDictionary<int, List<SysFieldGroup>> _groupsByTab = new();
    private readonly ConcurrentDictionary<int, List<SysField>> _fieldsByTab = new();
    private readonly ConcurrentDictionary<int, SysMenu> _menuById = new();

    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);
    private bool _disposed;

    public event EventHandler<DictionaryChangedEventArgs>? DictionaryChanged;

    public MetadataGraph(string connectionString)
    {
        _connectionString = connectionString;
        LoadAll();
    }

    private void LoadAll()
    {
        _loadSemaphore.Wait();
        try
        {
            using var connection = new Npgsql.NpgsqlConnection(_connectionString);
            LoadTables(connection);
            LoadReferences(connection);
            LoadValRules(connection);
            LoadColumns(connection);
            LoadWindows(connection);
            LoadTabs(connection);
            LoadFieldGroups(connection);
            LoadFields(connection);
            LoadMenus(connection);
        }
        finally
        {
            _loadSemaphore.Release();
        }
    }

    private void LoadWindows(Npgsql.NpgsqlConnection conn)
    {
        var windows = conn.Query<SysWindow>(
            @"SELECT ""SysWindow_ID"", ""ColumnName"", ""Name"", ""Description"", ""Help"",
                      ""DefaultTab_ID"" AS ""DefaultTabId"", ""AccessLevel"", ""IsView"",
                      ""EntityType"", ""IsActive"", ""CreatedBy"", ""CreatedAt"", ""UpdatedBy"", ""UpdatedAt""
               FROM ""SysWindow"" WHERE ""IsActive"" = true ORDER BY ""AccessLevel"", ""ColumnName""");

        foreach (var w in windows)
            _windows.TryAdd(w.ColumnName.ToLowerInvariant(), w);
    }

    private void LoadTabs(Npgsql.NpgsqlConnection conn)
    {
        var tabs = conn.Query<SysTab>(
            @"SELECT ""SysTab_ID"", ""SysWindow_ID"" AS ""SysWindowId"", ""SysTable_ID"" AS ""SysTableId"",
                      ""ColumnName"", ""Name"", ""SeqNo"", ""IsDefaultTab"", ""IsGrid"",
                      ""WhereClause"", ""IsDeleteable"", ""EntityType"", ""IsActive"",
                      ""CreatedBy"", ""CreatedAt"", ""UpdatedBy"", ""UpdatedAt""
               FROM ""SysTab"" WHERE ""IsActive"" = true ORDER BY ""SysWindow_ID"", ""SeqNo""");

        foreach (var t in tabs)
        {
            _tabsByWindow.AddOrUpdate(
                t.SysWindowId,
                _ => new List<SysTab> { t },
                (_, existing) =>
                {
                    existing.Add(t);
                    return existing;
                });
        }
    }

    private void LoadFieldGroups(Npgsql.NpgsqlConnection conn)
    {
        var groups = conn.Query<SysFieldGroup>(
            @"SELECT ""SysFieldGroup_ID"", ""SysTab_ID"" AS ""SysTabId"", ""ColumnName"",
                      ""Name"", ""SeqNo"", ""ColSpan"", ""IsCollapsed"", ""EntityType"", ""IsActive"",
                      ""CreatedBy"", ""CreatedAt"", ""UpdatedBy"", ""UpdatedAt""
               FROM ""SysFieldGroup"" WHERE ""IsActive"" = true ORDER BY ""SysTab_ID"", ""SeqNo""");

        foreach (var g in groups)
        {
            _groupsByTab.AddOrUpdate(
                g.SysTabId,
                _ => new List<SysFieldGroup> { g },
                (_, existing) =>
                {
                    existing.Add(g);
                    return existing;
                });
        }
    }

    private void LoadFields(Npgsql.NpgsqlConnection conn)
    {
        var fields = conn.Query<SysField>(
            @"SELECT ""SysField_ID"", ""SysTab_ID"" AS ""SysTabId"", ""SysColumn_ID"" AS ""SysColumnId"",
                      ""ColumnName"", ""Name"", ""ControlType"", ""SysFieldGroup_ID"" AS ""SysFieldGroupId"",
                      ""SeqNo"", ""IsMandatoryOverride"", ""IsReadOnlyOverride"",
                      ""ColSpan"", ""RowSpan"", ""DisplayLogic"", ""ReadOnlyLogic"", ""MandatoryLogic"",
                      ""DefaultValue"", ""EntityType"", ""IsActive"",
                      ""CreatedBy"", ""CreatedAt"", ""UpdatedBy"", ""UpdatedAt""
               FROM ""SysField"" WHERE ""IsActive"" = true ORDER BY ""SysTab_ID"", ""SeqNo""");

        foreach (var f in fields)
        {
            _fieldsByTab.AddOrUpdate(
                f.SysTabId,
                _ => new List<SysField> { f },
                (_, existing) =>
                {
                    existing.Add(f);
                    return existing;
                });
        }
    }

    private void LoadMenus(Npgsql.NpgsqlConnection conn)
    {
        var menus = conn.Query<SysMenu>(
            @"SELECT ""SysMenu_ID"", ""Parent_ID"" AS ""ParentId"", ""ColumnName"", ""Name"",
                      ""Icon"", ""Sequence"", ""Window_ID"" AS ""WindowId"", ""Process_ID"" AS ""ProcessId"",
                      ""IsSeparator"", ""IsSystem"", ""EntityType"", ""IsActive"",
                      ""CreatedBy"", ""CreatedAt"", ""UpdatedBy"", ""UpdatedAt""
               FROM ""SysMenu"" WHERE ""IsActive"" = true ORDER BY ""Sequence""");

        foreach (var m in menus)
            _menuById.TryAdd(m.SysMenuId, m);
    }

    private void LoadTables(Npgsql.NpgsqlConnection conn)
    {
        var tables = conn.Query<SysTable>(
            @"SELECT ""SysTable_ID"", ""TableName"", ""ClassName"", ""Description"",
                      ""IsView"", ""AccessLevel"", ""IsChangeLog"", ""IsDeleteable"",
                      ""IsHighVolume"", ""ReplicationType"", ""SysWindow_ID"" AS ""SysWindowId"",
                      ""EntityType"", ""IsActive""
               FROM ""SysTable"" WHERE ""IsActive"" = true ORDER BY ""TableName""");

        foreach (var t in tables)
        {
            _tables.TryAdd(t.TableName.ToLowerInvariant(), new TableMetadata
            {
                SysTableId = t.SysTableId,
                TableName = t.TableName,
                ClassName = t.ClassName,
                Description = t.Description,
                IsActive = t.IsActive
            });
        }
    }

    private void LoadReferences(Npgsql.NpgsqlConnection conn)
    {
        var rows = conn.Query<SysReference, SysReferenceList?, (SysReference Reference, SysReferenceList? List)>(
            @"SELECT r.""SysReference_ID"" AS ""SysReferenceId"", r.""Name"", r.""ValidationType"", r.""IsSystemType"", r.""ValueFormat"", r.""IsActive"",
                     l.""SysReferenceList_ID"" AS ""SysReferenceListId"", l.""SysReference_ID"" AS ""SysReferenceId"",
                     l.""Value"", l.""Name"" AS ""ListName"", l.""SeqNo"", l.""IsActive""
              FROM ""SysReference"" r
              LEFT JOIN ""SysReferenceList"" l ON r.""SysReference_ID"" = l.""SysReference_ID"" AND l.""IsActive"" = true
              WHERE r.""IsActive"" = true
              ORDER BY r.""Name"", l.""SeqNo""",
            (refEntity, list) => (refEntity, list),
            splitOn: "SysReferenceListId");

        var grouped = new Dictionary<string, (SysReference Reference, List<SysReferenceList> Lists)>();
        foreach (var item in rows)
        {
            var refName = item.Reference.Name.ToLowerInvariant();
            if (!grouped.TryGetValue(refName, out var group))
            {
                group = (item.Reference, new List<SysReferenceList>());
                grouped[refName] = group;
            }

            if (item.List != null && item.List.SysReferenceListId > 0)
            {
                group.Lists.Add(new SysReferenceList
                {
                    SysReferenceListId = item.List.SysReferenceListId,
                    SysReferenceId = item.List.SysReferenceId,
                    Value = item.List.Value,
                    Name = item.List.Name,
                    SeqNo = item.List.SeqNo,
                    IsActive = item.List.IsActive
                });
            }
        }

        foreach (var kv in grouped)
        {
            _references.TryAdd(kv.Key, new List<SysReference> { kv.Value.Reference });
            if (kv.Value.Lists.Count > 0)
            {
                _referenceValues.TryAdd(kv.Key, kv.Value.Lists);
            }

            // Map reference ID -> base type name for MetaColumn enrichment
            _refBaseTypes.TryAdd(kv.Value.Reference.SysReferenceId.ToString(), kv.Value.Reference.Name);
        }
    }

    private void LoadValRules(Npgsql.NpgsqlConnection conn)
    {
        var rules = conn.Query<SysValRule>(
            @"SELECT ""SysValRule_ID"", ""Name"", ""Description"", ""RuleType"", ""Code"", ""IsActive""
              FROM ""SysValRule"" WHERE ""IsActive"" = true");

        foreach (var rule in rules)
        {
            _valRulesById[rule.SysValRuleId] = rule;
        }
    }

    private void LoadColumns(Npgsql.NpgsqlConnection conn)
    {
        // Batch load tables — map SysTableId -> TableName
        var tableById = conn.Query(@"SELECT ""SysTable_ID"", ""TableName"" FROM ""SysTable"" WHERE ""IsActive"" = true")
            .ToDictionary(r => (int)r.SysTable_ID);

        // Batch load SysReference — map SysReferenceId -> (Name, ValidationType)
        var refById = conn.Query(@"SELECT ""SysReference_ID"", ""Name"", ""ValidationType"" FROM ""SysReference"" WHERE ""IsActive"" = true")
            .ToDictionary(r => (int)r.SysReference_ID);

        // Batch load SysColumn with SysElement display info
        var rows = conn.Query(@"SELECT c.""SysColumn_ID"", c.""SysTable_ID"", c.""ColumnName"", c.""SysReference_ID"",
                     c.""SysValRule_ID"", c.""SysReferenceValue_ID"", c.""FieldLength"",
                     c.""IsMandatory"", c.""IsKey"", c.""IsParent"", c.""IsIdentifier"",
                     c.""IsSelectionColumn"", c.""IsEncrypted"", c.""IsUpdateable"",
                     c.""IsAlwaysUpdateable"", c.""DefaultValue"", c.""ValueMin"", c.""ValueMax"",
                     c.""SeqNo"", c.""EntityType"", c.""IsActive"",
                     e.""Name"" AS ""ElementName"", e.""Help""
              FROM ""SysColumn"" c
              LEFT JOIN ""SysElement"" e ON c.""SysElement_ID"" = e.""SysElement_ID""
              WHERE c.""IsActive"" = true
              ORDER BY c.""SysTable_ID"", c.""SeqNo""");

        foreach (var row in rows)
        {
            var sysColumnId = (int)row.SysColumn_ID;
            var sysTableId = (int)row.SysTable_ID;
            var columnName = (string)row.ColumnName;
            var sysReferenceId = (int?)row.SysReference_ID;
            var sysValRuleId = (int?)row.SysValRule_ID;
            var fieldLength = (int?)row.FieldLength;
            var isMandatory = (bool)row.IsMandatory;
            var isKey = (bool)row.IsKey;
            var isUpdateable = (bool)row.IsUpdateable;
            var valueMin = row.ValueMin as string;
            var valueMax = row.ValueMax as string;
            var defaultValue = row.DefaultValue as string;
            var seqNo = (int)row.SeqNo;
            var isActive = (bool)row.IsActive;
            var elementName = row.ElementName as string;
            var elementHelp = row.Help as string;

            // Resolve table from batch-loaded dictionary (O(1) lookup)
            if (!tableById.TryGetValue(sysTableId, out var tableRow))
            {
                continue;
            }

            var tableName = (string)tableRow.TableName;
            var tableKey = tableName.ToLowerInvariant();

            // Resolve ValRule info (already loaded into _valRulesById)
            ValRuleTypeEnum valRuleType = 0;
            string? valRuleCode = null;
            if (sysValRuleId.HasValue)
            {
                if (_valRulesById.TryGetValue(sysValRuleId.Value, out var rule))
                {
                    valRuleType = rule.RuleType;
                    valRuleCode = rule.Code;
                }
            }

            // Resolve reference info from batch-loaded dictionary
            string? baseType = "VarChar";
            string? validationType = null;
            string? refName = null;
            if (sysReferenceId.HasValue && refById.TryGetValue(sysReferenceId.Value, out var refInfo))
            {
                refName = (string)refInfo.Name;
                validationType = (string)refInfo.ValidationType;
                baseType = refName;
            }

            var metaCol = new MetaColumn
            {
                SysColumnId = sysColumnId,
                SysTableId = sysTableId,
                TableName = tableName,
                ColumnName = columnName,
                Label = elementName ?? columnName,
                Help = elementHelp,
                BaseType = baseType ?? string.Empty,
                ValidationType = validationType,
                SysReferenceId = sysReferenceId,
                SysValRuleId = sysValRuleId,
                ValRuleType = valRuleType,
                ValRuleCode = valRuleCode,
                FieldLength = fieldLength,
                IsMandatory = isMandatory,
                IsKey = isKey,
                IsUpdateable = isUpdateable,
                ValueMin = valueMin,
                ValueMax = valueMax,
                DefaultValue = defaultValue,
                ReferenceName = refName,
                SeqNo = seqNo,
                IsActive = isActive
            };

            _tableColumns.AddOrUpdate(
                tableKey,
                _ => new List<MetaColumn> { metaCol },
                (_, existing) =>
                {
                    existing.Add(metaCol);
                    return existing;
                });
        }
    }

    public MetaColumn? GetColumn(string tableName, string columnName)
    {
        var key = tableName.ToLowerInvariant();
        var columns = _tableColumns.GetValueOrDefault(key);
        if (columns == null) return null;

        return columns.FirstOrDefault(c => string.Equals(c.ColumnName, columnName, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<MetaColumn> GetColumns(string tableName)
    {
        var key = tableName.ToLowerInvariant();
        var columns = _tableColumns.GetValueOrDefault(key);
        return columns != null ? (IReadOnlyList<MetaColumn>)new List<MetaColumn>(columns) : Array.Empty<MetaColumn>();
    }

    public IReadOnlyList<string> GetTableNames()
    {
        return _tables.Keys.ToList().AsReadOnly();
    }

    public IReadOnlyList<MetaColumn> GetAllColumns()
    {
        var all = new List<MetaColumn>();
        foreach (var columns in _tableColumns.Values)
        {
            all.AddRange(columns);
        }
        return all.AsReadOnly();
    }

    public TableMetadata? GetTable(string tableName)
    {
        return _tables.GetValueOrDefault(tableName.ToLowerInvariant());
    }

    public IReadOnlyList<SysReference> GetReferences(string referenceName)
    {
        var key = referenceName.ToLowerInvariant();
        var list = _references.GetValueOrDefault(key);
        return list != null ? (IReadOnlyList<SysReference>)list : Array.Empty<SysReference>();
    }

    public IReadOnlyList<SysReferenceList> GetReferenceValues(string referenceName)
    {
        var key = referenceName.ToLowerInvariant();
        var list = _referenceValues.GetValueOrDefault(key);
        return list != null ? (IReadOnlyList<SysReferenceList>)list : Array.Empty<SysReferenceList>();
    }

    internal SysValRule? GetValRuleById(int id)
    {
        return _valRulesById.GetValueOrDefault(id);
    }

    public TableMetadata? GetTableById(int tableId)
    {
        // Iterate through all tables to find by ID (O(n), but only called during lifecycle operations)
        foreach (var kv in _tables)
        {
            if (kv.Value.SysTableId == tableId)
            {
                return kv.Value;
            }
        }
        return null;
    }

    // UI metadata implementations
    public WindowMetadata? GetWindow(string columnName)
    {
        var key = columnName.ToLowerInvariant();
        var window = _windows.GetValueOrDefault(key);
        if (window == null) return null;

        return new WindowMetadata
        {
            SysWindowId = window.SysWindowId,
            ColumnName = window.ColumnName,
            Name = window.Name,
            Description = window.Description,
            Help = window.Help,
            DefaultTabId = window.DefaultTabId,
            AccessLevel = window.AccessLevel,
            IsView = window.IsView,
            EntityType = window.EntityType
        };
    }

    public IReadOnlyList<WindowMetadata> GetWindows()
    {
        var result = new List<WindowMetadata>();
        foreach (var w in _windows.Values)
        {
            result.Add(new WindowMetadata
            {
                SysWindowId = w.SysWindowId,
                ColumnName = w.ColumnName,
                Name = w.Name,
                Description = w.Description,
                Help = w.Help,
                DefaultTabId = w.DefaultTabId,
                AccessLevel = w.AccessLevel,
                IsView = w.IsView,
                EntityType = w.EntityType
            });
        }
        return result.AsReadOnly();
    }

    public IReadOnlyList<SysTab> GetTabs(int windowId)
    {
        var tabs = _tabsByWindow.GetValueOrDefault(windowId);
        return tabs != null ? (IReadOnlyList<SysTab>)new List<SysTab>(tabs) : Array.Empty<SysTab>();
    }

    public IReadOnlyList<SysFieldGroup> GetFieldGroups(int tabId)
    {
        var groups = _groupsByTab.GetValueOrDefault(tabId);
        return groups != null ? (IReadOnlyList<SysFieldGroup>)new List<SysFieldGroup>(groups) : Array.Empty<SysFieldGroup>();
    }

    public IReadOnlyList<SysField> GetFields(int tabId)
    {
        var fields = _fieldsByTab.GetValueOrDefault(tabId);
        return fields != null ? (IReadOnlyList<SysField>)new List<SysField>(fields) : Array.Empty<SysField>();
    }

    public IReadOnlyList<SysField> GetFieldsByTabAndGroupId(int tabId, int groupId)
    {
        var allFields = _fieldsByTab.GetValueOrDefault(tabId);
        if (allFields == null) return Array.Empty<SysField>();

        return allFields
            .Where(f => f.SysFieldGroupId == groupId)
            .OrderBy(f => f.SeqNo)
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<SysMenu> GetMenuHierarchy()
    {
        return _menuById.Values.ToList().AsReadOnly();
    }

    internal void OnDictionaryChanged(DictionaryChangedEvent @event)
    {
        var handler = DictionaryChanged;
        if (handler == null) return;

        var args = new DictionaryChangedEventArgs(@event.EntityType, @event.EntityId, @event.ChangeType);
        foreach (var sub in handler.GetInvocationList())
        {
            try
            {
                ((EventHandler<DictionaryChangedEventArgs>)sub).Invoke(this, args);
            }
            catch
            {
                // Never let a bad subscriber crash the publisher
            }
        }
    }

    internal void InvalidateTableColumns(string tableName)
    {
        _tableColumns.TryRemove(tableName.ToLowerInvariant(), out _);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _loadSemaphore.Dispose();
            _disposed = true;
        }
    }
}
