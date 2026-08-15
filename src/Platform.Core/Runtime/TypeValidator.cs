using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// Metadata-driven base type validator.
/// Validates value against the type defined in SysReference (VarChar, Integer, Decimal, etc.).
/// Does NOT know about business logic — only applies type-level constraints.
/// All type names match SysReference.Name entries seeded in Phase 1.
/// </summary>
public class TypeValidator : ITypeValidator
{
    private static readonly HashSet<string> SupportedTypes = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "VarChar", "Integer", "BigInt", "Decimal", "DateTime", "Date", "Time",
        "Boolean", "Uuid", "Text", "Binary"
    };

    public ColumnValidationResult Validate(string columnName, object? value, int? fieldLength, string baseType, string? valueMin = null, string? valueMax = null)
    {
        if (value == null)
        {
            return ColumnValidationResult.Success;
        }

        var errors = new List<string>();

        // 1. Check base type is supported
        if (!SupportedTypes.Contains(baseType))
        {
            errors.Add($"Unsupported base type '{baseType}' for column '{columnName}'.");
            return ColumnValidationResult.Fail(errors);
        }

        // 2. Validate based on type
        switch (baseType.ToLowerInvariant())
        {
            case "varchar":
            case "text":
            case "binary":
                ValidateString(columnName, value, fieldLength, errors);
                break;

            case "integer":
                ValidateInteger(columnName, value, valueMin, valueMax, errors);
                break;

            case "bigint":
                ValidateBigInt(columnName, value, valueMin, valueMax, errors);
                break;

            case "decimal":
                ValidateDecimal(columnName, value, fieldLength, valueMin, valueMax, errors);
                break;

            case "datetime":
                ValidateDateTime(columnName, value, valueMin, valueMax, errors, useTime: true);
                break;

            case "date":
                ValidateDateTime(columnName, value, valueMin, valueMax, errors, useTime: false);
                break;

            case "time":
                ValidateTime(columnName, value, valueMin, valueMax, errors);
                break;

            case "boolean":
                ValidateBoolean(columnName, value, errors);
                break;

            case "uuid":
                ValidateUuid(columnName, value, errors);
                break;
        }

        return errors.Count > 0 ? ColumnValidationResult.Fail(errors) : ColumnValidationResult.Success;
    }

    private static void ValidateString(string columnName, object value, int? fieldLength, List<string> errors)
    {
        if (value is string s)
        {
            if (fieldLength.HasValue && s.Length > fieldLength.Value)
            {
                errors.Add($"Column '{columnName}' exceeds maximum length of {fieldLength.Value}.");
            }
        }
        else
        {
            errors.Add($"Column '{columnName}' expects a string value.");
        }
    }

    private static void ValidateInteger(string columnName, object value, string? valueMin, string? valueMax, List<string> errors)
    {
        if (!int.TryParse(value.ToString(), out _))
        {
            errors.Add($"Column '{columnName}' expects an integer value.");
            return;
        }

        if (value is int intValue)
        {
            if (valueMin != null && intValue < int.Parse(valueMin))
            {
                errors.Add($"Column '{columnName}' value {intValue} is below minimum {valueMin}.");
            }
            if (valueMax != null && intValue > int.Parse(valueMax))
            {
                errors.Add($"Column '{columnName}' value {intValue} exceeds maximum {valueMax}.");
            }
        }
    }

    private static void ValidateBigInt(string columnName, object value, string? valueMin, string? valueMax, List<string> errors)
    {
        if (!long.TryParse(value.ToString(), out _))
        {
            errors.Add($"Column '{columnName}' expects a BigInt value.");
            return;
        }

        if (value is long longValue)
        {
            if (valueMin != null && longValue < long.Parse(valueMin))
            {
                errors.Add($"Column '{columnName}' value {longValue} is below minimum {valueMin}.");
            }
            if (valueMax != null && longValue > long.Parse(valueMax))
            {
                errors.Add($"Column '{columnName}' value {longValue} exceeds maximum {valueMax}.");
            }
        }
    }

    private static void ValidateDecimal(string columnName, object value, int? precision, string? valueMin, string? valueMax, List<string> errors)
    {
        if (!decimal.TryParse(value.ToString(), out _))
        {
            errors.Add($"Column '{columnName}' expects a Decimal value.");
            return;
        }

        if (value is decimal decValue)
        {
            if (precision.HasValue)
            {
                // precision = total digits
                var digits = decValue.ToString().Replace("-", "").Replace(".", "").Length;
                if (digits > precision.Value)
                {
                    errors.Add($"Column '{columnName}' exceeds maximum precision of {precision.Value}.");
                }
            }

            if (valueMin != null && decValue < decimal.Parse(valueMin))
            {
                errors.Add($"Column '{columnName}' value {decValue} is below minimum {valueMin}.");
            }
            if (valueMax != null && decValue > decimal.Parse(valueMax))
            {
                errors.Add($"Column '{columnName}' value {decValue} exceeds maximum {valueMax}.");
            }
        }
    }

    private static void ValidateDateTime(string columnName, object value, string? valueMin, string? valueMax, List<string> errors, bool useTime)
    {
        DateTime expected;
        if (useTime)
        {
            if (!DateTime.TryParse(value.ToString(), out expected))
            {
                errors.Add($"Column '{columnName}' expects a DateTime value.");
                return;
            }
        }
        else
        {
            if (!DateOnly.TryParse(value.ToString(), out var dateOnly))
            {
                // Try DateTime as fallback
                if (!DateTime.TryParse(value.ToString(), out expected))
                {
                    errors.Add($"Column '{columnName}' expects a Date value.");
                    return;
                }
                return;
            }
            return;
        }

        if (value is DateTime dt)
        {
            if (valueMin != null && dt < DateTime.Parse(valueMin))
            {
                errors.Add($"Column '{columnName}' value is before minimum {valueMin}.");
            }
            if (valueMax != null && dt > DateTime.Parse(valueMax))
            {
                errors.Add($"Column '{columnName}' value exceeds maximum {valueMax}.");
            }
        }
    }

    private static void ValidateTime(string columnName, object value, string? valueMin, string? valueMax, List<string> errors)
    {
        if (!TimeOnly.TryParse(value.ToString(), out _))
        {
            errors.Add($"Column '{columnName}' expects a Time value.");
        }
    }

    private static void ValidateBoolean(string columnName, object value, List<string> errors)
    {
        if (!(value is bool || value is string s && (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "1") || string.Equals(s, "0"))))
        {
            errors.Add($"Column '{columnName}' expects a Yes/No (Boolean) value.");
        }
    }

    private static void ValidateUuid(string columnName, object value, List<string> errors)
    {
        if (!Guid.TryParse(value.ToString(), out _))
        {
            errors.Add($"Column '{columnName}' expects a Uuid (GUID) value.");
        }
    }
}
