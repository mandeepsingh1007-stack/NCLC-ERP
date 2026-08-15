using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// Orchestrates the full validation pipeline for a single column value.
/// Non-negotiable order: mandatory -> type -> length -> min/max -> reference -> ValRule.
/// Returns ALL failures, not just the first.
/// </summary>
public class POValidator
{
    private readonly ITypeValidator _typeValidator;
    private readonly IReferenceValueValidator _referenceValidator;
    private readonly IValRuleEngine _valRuleEngine;
    private readonly IMetadataGraph _metadataGraph;

    public POValidator(
        ITypeValidator typeValidator,
        IReferenceValueValidator referenceValidator,
        IValRuleEngine valRuleEngine,
        IMetadataGraph metadataGraph)
    {
        _typeValidator = typeValidator;
        _referenceValidator = referenceValidator;
        _valRuleEngine = valRuleEngine;
        _metadataGraph = metadataGraph;
    }

    /// <summary>
    /// Validates a value against all rules defined for the given MetaColumn.
    /// Returns ALL errors from ALL validation steps, not just the first.
    /// </summary>
    public ColumnValidationResult Validate(string tableName, MetaColumn metaColumn, object? value, IReadOnlyContext context)
    {
        var errors = new List<string>();

        // 1. Mandatory check
        if (metaColumn.IsMandatory && (value == null || (value is string s && string.IsNullOrWhiteSpace(s))))
        {
            errors.Add($"{metaColumn.Label} is required.");
            return ColumnValidationResult.Fail(errors);
        }

        // Skip remaining checks for null/empty non-mandatory values
        if (value == null || (value is string s2 && string.IsNullOrWhiteSpace(s2)))
        {
            return ColumnValidationResult.Success;
        }

        // 2. Base type validation
        var typeResult = _typeValidator.Validate(
            metaColumn.ColumnName, value, metaColumn.FieldLength, metaColumn.BaseType,
            metaColumn.ValueMin, metaColumn.ValueMax);
        if (!typeResult.IsSuccess)
        {
            errors.AddRange(typeResult.Errors);
        }

        // 3. Reference validation (LIST/TABLE)
        if (metaColumn.ValidationType != null && metaColumn.ValidationType != "SEARCH" && metaColumn.SysReferenceId.HasValue)
        {
            var refResult = _referenceValidator.Validate(
                metaColumn.ColumnName, value, metaColumn.SysReferenceId, metaColumn.ValidationType, tableName, metaColumn.ReferenceName);
            if (!refResult.IsSuccess)
            {
                errors.AddRange(refResult.Errors);
            }
        }

        // 4. ValRule evaluation
        if (metaColumn.SysValRuleId.HasValue && metaColumn.ValRuleCode != null)
        {
            var sysRule = new SysValRule
            {
                SysValRuleId = metaColumn.SysValRuleId.Value,
                Name = metaColumn.Label,
                RuleType = metaColumn.ValRuleType,
                Code = metaColumn.ValRuleCode
            };

            var valResult = _valRuleEngine.Evaluate(sysRule, value, context);
            if (!valResult.Passed)
            {
                errors.Add(valResult.ErrorMessage ?? $"Validation rule '{valResult.RuleName}' failed.");
            }
        }

        return errors.Count > 0 ? ColumnValidationResult.Fail(errors) : ColumnValidationResult.Success;
    }

    /// <summary>
    /// Validates all columns for a table. Returns all errors from all columns.
    /// </summary>
    public ColumnValidationResult ValidateAll(string tableName, IReadOnlyDictionary<string, object?> values, IReadOnlyContext context)
    {
        var columns = _metadataGraph.GetColumns(tableName);
        var allErrors = new List<string>();

        foreach (var metaCol in columns.Where(c => c.IsActive))
        {
            if (!values.TryGetValue(metaCol.ColumnName, out var value))
            {
                // Mandatory check for missing values
                if (metaCol.IsMandatory)
                {
                    allErrors.Add($"{metaCol.Label} is required.");
                }
                continue;
            }

            var result = Validate(tableName, metaCol, value, context);
            if (!result.IsSuccess)
            {
                allErrors.AddRange(result.Errors);
            }
        }

        return allErrors.Count > 0 ? ColumnValidationResult.Fail(allErrors) : ColumnValidationResult.Success;
    }

    /// <summary>
    /// Validates all columns on an IPersistentObject using reflection to extract property values.
    /// Returns all errors from all columns. Used by POLifecycleManager before persist.
    /// </summary>
    public ColumnValidationResult ValidatePO(IPersistentObject po, IReadOnlyContext context)
    {
        var tableInfo = _metadataGraph.GetTableById(po.SysTableId);
        if (tableInfo == null)
        {
            return ColumnValidationResult.Fail("Unknown SysTableId.");
        }

        var props = po.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var values = new Dictionary<string, object?>();
        foreach (var prop in props)
        {
            // Map property names to column names — accept both PascalCase and ColumnName
            values[prop.Name] = prop.GetValue(po);
        }

        return ValidateAll(tableInfo.TableName, values, context);
    }
}
