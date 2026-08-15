using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// Metadata-driven reference validation.
/// LIST: check value against SysReferenceList entries.
/// TABLE: check FK existence in target table via sys reference table mapping.
/// SEARCH: deferred in Phase 2 — always passes.
/// </summary>
public class ReferenceValueValidator : IReferenceValueValidator
{
    private readonly IMetadataGraph _metadataGraph;

    public ReferenceValueValidator(IMetadataGraph metadataGraph)
    {
        _metadataGraph = metadataGraph;
    }

    public ColumnValidationResult Validate(string columnName, object? value, int? sysReferenceId, string? validationType, string? tableName, string? referenceName)
    {
        // TABLE validation: reject null/empty first, before general null/empty passthrough
        if (validationType == "TABLE" && sysReferenceId.HasValue && tableName != null)
        {
            string? emptyVal = value as string;
            if (value == null || (emptyVal != null && string.IsNullOrWhiteSpace(emptyVal)))
            {
                return ColumnValidationResult.Fail($"Column '{columnName}' value cannot be empty for a TABLE reference.");
            }
            return ValidateTableReference(columnName, value.ToString() ?? string.Empty, sysReferenceId.Value, tableName);
        }

        if (value == null || (value is string s && string.IsNullOrWhiteSpace(s)))
        {
            return ColumnValidationResult.Success;
        }

        var valueStr = value.ToString() ?? string.Empty;

        if (validationType == "SEARCH")
        {
            // SEARCH is deferred in Phase 2 — always passes
            return ColumnValidationResult.Success;
        }

        if (validationType == "LIST" && sysReferenceId.HasValue)
        {
            return ValidateList(columnName, valueStr, referenceName ?? sysReferenceId.ToString() ?? string.Empty);
        }

        if (validationType == "TABLE" && sysReferenceId.HasValue && tableName != null)
        {
            return ValidateTableReference(columnName, valueStr, sysReferenceId.Value, tableName);
        }

        // No validation type defined — pass through
        return ColumnValidationResult.Success;
    }

    private ColumnValidationResult ValidateList(string columnName, string value, string referenceName)
    {
        // Use reference NAME (not ID) — GetReferences expects a name
        var references = _metadataGraph.GetReferences(referenceName);
        if (references.Count > 0)
        {
            var reference = references[0];
            var listValues = _metadataGraph.GetReferenceValues(reference.Name);
            if (listValues.Count > 0)
            {
                var validValues = listValues.Select(l => l.Value).ToHashSet();
                if (!validValues.Contains(value))
                {
                    return ColumnValidationResult.Fail(
                        $"Column '{columnName}' value '{value}' is not in the allowed list.");
                }
            }
        }

        return ColumnValidationResult.Success;
    }

    private ColumnValidationResult ValidateTableReference(string columnName, string value, int sysReferenceId, string tableName)
    {
        // TABLE validation requires a DB query to verify FK existence in the target table.
        // For Phase 2: at minimum validate the value is non-empty and well-formed.
        // Full FK value validation is deferred to Phase 3.
        if (string.IsNullOrEmpty(value))
        {
            return ColumnValidationResult.Fail($"Column '{columnName}' value cannot be empty for a TABLE reference.");
        }

        return ColumnValidationResult.Success;
    }
}
