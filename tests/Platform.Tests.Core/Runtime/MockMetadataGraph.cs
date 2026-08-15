using Platform.Core.Metadata;
using Platform.Core.Runtime;

namespace Platform.Tests.Core.Runtime;

/// <summary>
/// In-memory mock of IMetadataGraph for unit tests that do not require a database.
/// </summary>
public class MockMetadataGraph : IMetadataGraph
{
    private readonly List<MetaColumn> _columns = new();
    private readonly List<TableMetadata> _tables = new();
    private readonly Dictionary<string, SysReference> _references = new();
    private readonly List<SysReferenceList> _referenceLists = new();

    public void AddColumn(MetaColumn col) => _columns.Add(col);
    public void AddTable(TableMetadata table) => _tables.Add(table);
    public void AddReference(SysReference reference)
    {
        _references[reference.Name] = reference;
        _references[reference.SysReferenceId.ToString()] = reference;
    }
    public void AddReferenceList(SysReferenceList list) => _referenceLists.Add(list);

    public MetaColumn? GetColumn(string tableName, string columnName)
        => _columns.FirstOrDefault(c => c.TableName == tableName && c.ColumnName == columnName);

    public IReadOnlyList<MetaColumn> GetColumns(string tableName)
        => _columns.Where(c => c.TableName == tableName).ToList().AsReadOnly();

    public IReadOnlyList<string> GetTableNames()
        => _tables.Select(t => t.TableName).ToList().AsReadOnly();

    public IReadOnlyList<MetaColumn> GetAllColumns() => _columns.AsReadOnly();

    public TableMetadata? GetTable(string tableName)
        => _tables.FirstOrDefault(t => t.TableName == tableName);

    public IReadOnlyList<SysReference> GetReferences(string key)
        => _references.TryGetValue(key, out var refInfo) ? new[] { refInfo }.ToList() : new List<SysReference>();

    public IReadOnlyList<SysReferenceList> GetReferenceValues(string referenceName)
    {
        if (!_references.TryGetValue(referenceName, out var refInfo))
            return new List<SysReferenceList>();

        return _referenceLists.Where(r => r.SysReferenceId == refInfo.SysReferenceId).ToList().AsReadOnly();
    }

    public TableMetadata? GetTableById(int tableId)
        => _tables.FirstOrDefault(t => t.SysTableId == tableId);

    // UI metadata implementations (always empty for unit test mock)
    public WindowMetadata? GetWindow(string columnName) => null;
    public IReadOnlyList<WindowMetadata> GetWindows() => Array.Empty<WindowMetadata>();
    public IReadOnlyList<SysTab> GetTabs(int windowId) => Array.Empty<SysTab>();
    public IReadOnlyList<SysFieldGroup> GetFieldGroups(int tabId) => Array.Empty<SysFieldGroup>();
    public IReadOnlyList<SysField> GetFields(int tabId) => Array.Empty<SysField>();
    public IReadOnlyList<SysField> GetFieldsByTabAndGroupId(int tabId, int groupId) => Array.Empty<SysField>();
    public IReadOnlyList<SysMenu> GetMenuHierarchy() => Array.Empty<SysMenu>();

    // DictionaryChanged is required by IMetadataGraph. It is intentionally not raised in unit tests
    // because the mock is stateless and never mutates metadata. The real MetadataGraph raises it
    // via OnDictionaryChanged() after mutation operations in production code.
#pragma warning disable CS0067 // Event is never used — required by interface, raised in production only
    public event EventHandler<DictionaryChangedEventArgs>? DictionaryChanged;
#pragma warning restore CS0067
}
