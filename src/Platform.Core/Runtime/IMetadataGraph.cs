using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

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
