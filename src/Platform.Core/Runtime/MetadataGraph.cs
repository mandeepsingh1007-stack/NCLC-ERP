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
        }
        finally
        {
            _loadSemaphore.Release();
        }
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
            @"SELECT r.""SysReference_ID"", r.""Name"", r.""ValidationType"", r.""IsSystemType"", r.""ValueFormat"",
                     l.""SysReferenceList_ID"", l.""SysReference_ID"",
                     l.""Value"", l.""Name"" AS ""ListName"", l.""SeqNo"", l.""IsActive""
              FROM ""SysReference"" r
              LEFT JOIN ""SysReferenceList"" l ON r.""SysReference_ID"" = l.""SysReference_ID"" AND l.""IsActive"" = true
              WHERE r.""IsActive"" = true
              ORDER BY r.""Name"", l.""SeqNo""",
            (refEntity, list) => (refEntity, list));

        var grouped = new Dictionary<string, (SysReference Reference, List<SysReferenceList> Lists)>();
        foreach (var item in rows)
        {
            var refName = item.Reference.Name.ToLowerInvariant();
            if (!grouped.TryGetValue(refName, out var group))
            {
                group = (item.Reference, new List<SysReferenceList>());
                grouped[refName] = group;
            }

            if (item.List != null)
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
        // Batch load tables — map SysTableId -> SysTable
        var tableById = conn.Query<SysTable>(
                @"SELECT ""SysTable_ID"", ""TableName"" FROM ""SysTable"" WHERE ""IsActive"" = true")
            .ToDictionary(t => t.SysTableId);

        // Batch load SysReference — map SysReferenceId -> SysReference
        var refById = conn.Query<SysReference>(
                @"SELECT ""SysReference_ID"", ""Name"", ""ValidationType"" FROM ""SysReference"" WHERE ""IsActive"" = true")
            .ToDictionary(r => r.SysReferenceId);

        var rows = conn.Query<SysColumn, SysElement?, (SysColumn Column, SysElement? Element)>(
            @"SELECT c.""SysColumn_ID"", c.""SysTable_ID"", c.""ColumnName"", c.""SysReference_ID"",
                     c.""SysValRule_ID"", c.""SysReferenceValue_ID"", c.""FieldLength"",
                     c.""IsMandatory"", c.""IsKey"", c.""IsParent"", c.""IsIdentifier"",
                     c.""IsSelectionColumn"", c.""IsEncrypted"", c.""IsUpdateable"",
                     c.""IsAlwaysUpdateable"", c.""DefaultValue"", c.""ValueMin"", c.""ValueMax"",
                     c.""SeqNo"", c.""EntityType"", c.""IsActive"",
                     e.""Name"" AS ""ElementName"", e.""Help""
              FROM ""SysColumn"" c
              LEFT JOIN ""SysElement"" e ON c.""SysElement_ID"" = e.""SysElement_ID""
              WHERE c.""IsActive"" = true
              ORDER BY c.""SysTable_ID"", c.""SeqNo""",
            (col, elem) => (col, elem));

        foreach (var row in rows)
        {
            var col = row.Column;
            // Resolve table from batch-loaded dictionary (O(1) lookup)
            if (!tableById.TryGetValue(col.SysTableId, out var table))
            {
                continue;
            }

            var tableName = table.TableName;
            var tableKey = tableName.ToLowerInvariant();

            // Resolve ValRule info (already loaded into _valRulesById)
            ValRuleTypeEnum valRuleType = 0;
            string? valRuleCode = null;
            if (col.SysValRuleId.HasValue)
            {
                if (_valRulesById.TryGetValue(col.SysValRuleId.Value, out var rule))
                {
                    valRuleType = rule.RuleType;
                    valRuleCode = rule.Code;
                }
            }

            // Resolve reference info from batch-loaded dictionary
            string? baseType = "VarChar";
            string? validationType = null;
            if (col.SysReferenceId.HasValue)
            {
                if (refById.TryGetValue(col.SysReferenceId.Value, out var refInfo))
                {
                    validationType = refInfo.ValidationType.ToString();
                    baseType = refInfo.Name;
                }
            }

            var metaCol = new MetaColumn
            {
                SysColumnId = col.SysColumnId,
                SysTableId = col.SysTableId,
                TableName = tableName,
                ColumnName = col.ColumnName,
                Label = row.Element?.Name ?? col.ColumnName,
                Help = row.Element?.Help,
                BaseType = baseType ?? string.Empty,
                ValidationType = validationType,
                SysReferenceId = col.SysReferenceId,
                SysValRuleId = col.SysValRuleId,
                ValRuleType = valRuleType,
                ValRuleCode = valRuleCode,
                FieldLength = col.FieldLength,
                IsMandatory = col.IsMandatory,
                IsKey = col.IsKey,
                IsUpdateable = col.IsUpdateable,
                ValueMin = col.ValueMin,
                ValueMax = col.ValueMax,
                DefaultValue = col.DefaultValue,
                ReferenceName = refById.TryGetValue(col.SysReferenceId ?? 0, out var refInfo2) ? refInfo2.Name : null,
                SeqNo = col.SeqNo,
                IsActive = col.IsActive
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
