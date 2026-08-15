using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// Metadata-driven reference validation.
/// LIST: check value against SysReferenceList entries.
/// TABLE: check FK existence in target table.
/// SEARCH: deferred in Phase 2 — always passes.
/// </summary>
public interface IReferenceValueValidator
{
    ColumnValidationResult Validate(string columnName, object? value, int? sysReferenceId, string? validationType, string? tableName, string? referenceName);
}
