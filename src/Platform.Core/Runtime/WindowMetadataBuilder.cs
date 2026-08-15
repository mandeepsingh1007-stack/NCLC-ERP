using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// Assembles the full JSON metadata contract for a window.
/// Reads from SysWindow + SysTab + SysField + SysFieldGroup + SysColumn + SysReference + SysValRule.
/// This is the data contract consumed by the GenericMetaApi and the frontend React Window component.
/// </summary>
public interface IWindowMetadataBuilder
{
    /// <summary>
    /// Builds the window metadata for a given window ID.
    /// Returns null if window not found.
    /// </summary>
    WindowContract? BuildWindow(int windowId);
}

/// <summary>
/// JSON contract for a window — consumed by frontend React components.
/// </summary>
public sealed class WindowContract
{
    public int WindowId { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Help { get; set; }

    public IReadOnlyList<TabContract> Tabs { get; init; } = Array.Empty<TabContract>();
}

/// <summary>
/// JSON contract for a tab.
/// </summary>
public sealed class TabContract
{
    public int TabId { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SysTableId { get; set; }
    public bool IsGrid { get; set; }
    public bool IsDefaultTab { get; set; }
    public string? WhereClause { get; set; }

    public IReadOnlyList<FieldContract> Fields { get; init; } = Array.Empty<FieldContract>();
    public IReadOnlyList<FieldGroupContract> FieldGroups { get; init; } = Array.Empty<FieldGroupContract>();
}

/// <summary>
/// JSON contract for a field.
/// </summary>
public sealed class FieldContract
{
    public string ColumnName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Help { get; set; }
    public string ControlType { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsMandatoryOverride { get; set; }
    public bool IsReadOnlyOverride { get; set; }
    public int ColSpan { get; set; }
    public int RowSpan { get; set; }
    public string? DefaultValue { get; set; }
    public string? DisplayLogic { get; set; }
    public string? ReadOnlyLogic { get; set; }
    public string? MandatoryLogic { get; set; }
    public string? FieldGroup { get; set; }

    public ReferenceInfo? SysReference { get; set; }
    public int? FieldLength { get; set; }
}

/// <summary>
/// JSON contract for a reference (SysReference info).
/// </summary>
public sealed class ReferenceInfo
{
    public string Name { get; set; } = string.Empty;
    public string ValidationType { get; set; } = string.Empty;
}

/// <summary>
/// JSON contract for a field group.
/// </summary>
public sealed class FieldGroupContract
{
    public string GroupName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int ColSpan { get; set; }
    public bool IsCollapsed { get; set; }
    public IReadOnlyList<string> FieldColumnNames { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Resolves the full window metadata contract from metadata tables.
/// Joins: SysWindow → SysTab → SysField → SysColumn → SysReference + SysValRule
/// </summary>
public class WindowMetadataBuilder : IWindowMetadataBuilder
{
    private readonly IMetadataGraph _metadataGraph;

    public WindowMetadataBuilder(IMetadataGraph metadataGraph)
    {
        _metadataGraph = metadataGraph;
    }

    public WindowContract? BuildWindow(int windowId)
    {
        // Load window via metadata graph
        var windows = _metadataGraph.GetWindows();
        var window = windows.FirstOrDefault(w => w.SysWindowId == windowId);
        if (window == null)
            return null;

        var tabs = _metadataGraph.GetTabs(windowId);
        var contract = new WindowContract
        {
            WindowId = window.SysWindowId,
            ColumnName = window.ColumnName,
            Name = window.Name,
            Description = window.Description,
            Help = window.Help,
            Tabs = BuildTabs(windowId, tabs).ToList()
        };

        return contract;
    }

    private IEnumerable<TabContract> BuildTabs(int windowId, IReadOnlyList<SysTab> tabs)
    {
        var result = new List<TabContract>();
        foreach (var tab in tabs.OrderBy(t => t.SeqNo))
        {
            var fields = _metadataGraph.GetFields(tab.SysTabId);
            var groups = _metadataGraph.GetFieldGroups(tab.SysTabId);

            var contract = new TabContract
            {
                TabId = tab.SysTabId,
                ColumnName = tab.ColumnName,
                Name = tab.Name,
                SysTableId = tab.SysTableId,
                IsGrid = tab.IsGrid,
                IsDefaultTab = tab.IsDefaultTab,
                WhereClause = tab.WhereClause,
                Fields = BuildFields(tab, fields, groups).ToList(),
                FieldGroups = BuildFieldGroups(tab.SysTabId, groups, fields).ToList()
            };

            result.Add(contract);
        }
        return result;
    }

    private IEnumerable<FieldContract> BuildFields(SysTab tab, IReadOnlyList<SysField> fields, IReadOnlyList<SysFieldGroup> groups)
    {
        var result = new List<FieldContract>();
        var groupMap = new Dictionary<int, string>();
        foreach (var g in groups)
            groupMap[g.SysFieldGroupId] = g.Name;

        foreach (var field in fields.OrderBy(f => f.SeqNo))
        {
            // Resolve column metadata from the graph
            var column = _metadataGraph.GetColumn(
                GetTableName(tab.SysTableId), field.ColumnName);

            var label = column?.Label ?? field.Name;
            var help = column?.Help;

            // Resolve control type from SysReference if available
            var controlType = ResolveControlType(column);
            var referenceInfo = column?.SysReferenceId.HasValue == true ? new ReferenceInfo
            {
                Name = column.BaseType,
                ValidationType = column.ValidationType ?? string.Empty,
            } : null;

            var groupKey = field.SysFieldGroupId;
            string? fieldGroup = null;
            if (groupKey.HasValue && groupMap.ContainsKey(groupKey.Value))
                fieldGroup = groupMap[groupKey.Value];

            var contract = new FieldContract
            {
                ColumnName = field.ColumnName,
                Label = label,
                Help = help,
                ControlType = controlType,
                IsMandatory = column?.IsMandatory ?? false || field.IsMandatoryOverride,
                IsReadOnly = !column?.IsUpdateable ?? false || field.IsReadOnlyOverride,
                IsMandatoryOverride = field.IsMandatoryOverride,
                IsReadOnlyOverride = field.IsReadOnlyOverride,
                ColSpan = field.ColSpan,
                RowSpan = field.RowSpan,
                DefaultValue = field.DefaultValue,
                DisplayLogic = field.DisplayLogic,
                ReadOnlyLogic = field.ReadOnlyLogic,
                MandatoryLogic = field.MandatoryLogic,
                FieldGroup = fieldGroup,
                SysReference = referenceInfo,
                FieldLength = column?.FieldLength
            };

            result.Add(contract);
        }
        return result;
    }

    private IEnumerable<FieldGroupContract> BuildFieldGroups(int tabId, IReadOnlyList<SysFieldGroup> groups, IReadOnlyList<SysField> fields)
    {
        // Build fields grouped by group ID
        var fieldByGroup = fields
            .GroupBy(f => f.SysFieldGroupId.GetValueOrDefault(-1))
            .ToDictionary(g => g.Key, g => new List<string>(g.Select(f => f.ColumnName)));

        var result = new List<FieldGroupContract>();

        // If no groups exist, don't return empty groups — frontend handles ungrouped fields
        foreach (var group in groups.OrderBy(g => g.SeqNo))
        {
            var groupFields = new List<string>();
            if (fieldByGroup.TryGetValue(group.SysFieldGroupId, out var fieldsList))
                groupFields = fieldsList;

            result.Add(new FieldGroupContract
            {
                GroupName = group.Name,
                Label = group.Name,
                ColSpan = group.ColSpan,
                IsCollapsed = group.IsCollapsed,
                FieldColumnNames = groupFields.AsReadOnly()
            });
        }

        return result;
    }

    private string GetTableName(int sysTableId)
    {
        var table = _metadataGraph.GetTableById(sysTableId);
        return table?.TableName ?? string.Empty;
    }

    private string ResolveControlType(MetaColumn? column)
    {
        if (column == null)
            return "TextInput";

        var validationType = column.ValidationType;

        return validationType switch
        {
            "LIST" => "ListDropdown",
            "TABLE" => "TableLookup",
            "SEARCH" => "SearchPopup",
            "BOOLEAN" or "yesNo" => "YesNoToggle",
            "INTEGER" or "BIGINT" => "NumberInput",
            "DATE" or "DATETIME" => "DateInput",
            "VARCHAR" or "TEXT" => "TextInput",
            _ => "TextInput"
        };
    }
}
