using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// UI metadata container for a single window.
/// </summary>
public sealed class WindowMetadata
{
    public int SysWindowId { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Help { get; set; }
    public int? DefaultTabId { get; set; }
    public int AccessLevel { get; set; }
    public bool IsView { get; set; }
    public string? EntityType { get; set; }
    public IReadOnlyList<SysTab> Tabs => Array.Empty<SysTab>();
}

/// <summary>
/// UI metadata container for a single tab.
/// </summary>
public sealed class TabMetadata
{
    public int SysTabId { get; set; }
    public int SysWindowId { get; set; }
    public int SysTableId { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SeqNo { get; set; }
    public bool IsDefaultTab { get; set; }
    public bool IsGrid { get; set; }
    public string? WhereClause { get; set; }
    public bool IsDeleteable { get; set; }
    public string? EntityType { get; set; }
    public IReadOnlyList<SysFieldGroup> Groups => Array.Empty<SysFieldGroup>();
    public IReadOnlyList<SysField> Fields => Array.Empty<SysField>();
}

/// <summary>
/// In-memory representation of all dictionary metadata.
/// Loaded once at startup, raises DictionaryChanged on mutations.
/// Thread-safe for reads.
/// </summary>
public interface IMetadataGraph
{
    MetaColumn? GetColumn(string tableName, string columnName);
    IReadOnlyList<MetaColumn> GetColumns(string tableName);
    IReadOnlyList<string> GetTableNames();
    IReadOnlyList<MetaColumn> GetAllColumns();
    TableMetadata? GetTable(string tableName);
    IReadOnlyList<SysReference> GetReferences(string referenceName);
    IReadOnlyList<SysReferenceList> GetReferenceValues(string referenceName);
    TableMetadata? GetTableById(int tableId);
    event EventHandler<DictionaryChangedEventArgs>? DictionaryChanged;

    // UI metadata
    WindowMetadata? GetWindow(string columnName);
    IReadOnlyList<WindowMetadata> GetWindows();
    IReadOnlyList<SysTab> GetTabs(int windowId);
    IReadOnlyList<SysFieldGroup> GetFieldGroups(int tabId);
    IReadOnlyList<SysField> GetFields(int tabId);
    IReadOnlyList<SysField> GetFieldsByTabAndGroupId(int tabId, int groupId);
    IReadOnlyList<SysMenu> GetMenuHierarchy();
}

/// <summary>
/// Lightweight container for table-level metadata info.
/// </summary>
public sealed class TableMetadata
{
    public int SysTableId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string? ClassName { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
