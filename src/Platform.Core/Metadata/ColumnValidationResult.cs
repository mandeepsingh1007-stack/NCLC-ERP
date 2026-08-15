using System.Collections.Immutable;

namespace Platform.Core.Metadata;

/// <summary>
/// Result of a single-column validation step. May contain multiple errors.
/// Renamed from ValidationResult to avoid conflict with System.ComponentModel.DataAnnotations.ValidationResult.
/// </summary>
public sealed class ColumnValidationResult
{
    public bool IsSuccess { get; }
    public IReadOnlyList<string> Errors { get; }

    private ColumnValidationResult(bool isSuccess, IReadOnlyList<string> errors)
    {
        IsSuccess = isSuccess;
        Errors = errors;
    }

    public static ColumnValidationResult Success => new(true, ImmutableList<string>.Empty);

    public static ColumnValidationResult Fail(IEnumerable<string> errors)
    {
        return new(false, errors.ToImmutableList());
    }

    public static ColumnValidationResult Fail(string error)
    {
        return Fail(new[] { error });
    }
}
