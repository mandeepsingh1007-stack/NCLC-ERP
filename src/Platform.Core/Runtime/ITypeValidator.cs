using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// Metadata-driven base type validator.
/// Validates value against the type defined in SysReference (VarChar, Integer, Decimal, etc.).
/// Does NOT know about business logic — only applies type-level constraints.
/// </summary>
public interface ITypeValidator
{
    ColumnValidationResult Validate(string columnName, object? value, int? fieldLength, string baseType, string? valueMin = null, string? valueMax = null);
}
