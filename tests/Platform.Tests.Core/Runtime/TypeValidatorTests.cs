using FluentAssertions;
using Platform.Core.Metadata;
using Platform.Core.Runtime;

namespace Platform.Tests.Core.Runtime;

public class TypeValidatorTests
{
    [Fact]
    public void Validate_NullValue_ShouldPass()
    {
        var validator = new TypeValidator();
        var result = validator.Validate("Name", null, 60, "VarChar");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_Varchar_ValidString_ShouldPass()
    {
        var validator = new TypeValidator();
        var result = validator.Validate("Name", "Hello World", 60, "VarChar");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_Varchar_ExceedsLength_ShouldFail()
    {
        var validator = new TypeValidator();
        var longValue = new string('a', 100);
        var result = validator.Validate("Name", longValue, 50, "VarChar");

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("exceeds maximum length");
    }

    [Fact]
    public void Validate_Varchar_ExactLength_ShouldPass()
    {
        var validator = new TypeValidator();
        var result = validator.Validate("Name", "abcde", 5, "VarChar");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_Varchar_NonString_ShouldFail()
    {
        var validator = new TypeValidator();
        var result = validator.Validate("Name", 123, 60, "VarChar");

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("expects a string value");
    }

    [Fact]
    public void Validate_Integer_Valid_ShouldPass()
    {
        var validator = new TypeValidator();
        var result = validator.Validate("Age", 42, null, "Integer");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_Integer_Invalid_ShouldFail()
    {
        var validator = new TypeValidator();
        var result = validator.Validate("Age", "abc", null, "Integer");

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("expects an integer value");
    }

    [Fact]
    public void Validate_Integer_BelowMinimum_ShouldFail()
    {
        var validator = new TypeValidator();
        var result = validator.Validate("Age", -1, null, "Integer", valueMin: "0");

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("below minimum");
    }

    [Fact]
    public void Validate_Integer_AboveMaximum_ShouldFail()
    {
        var validator = new TypeValidator();
        var result = validator.Validate("Age", 150, null, "Integer", valueMax: "120");

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("exceeds maximum");
    }

    [Fact]
    public void Validate_BigInt_Valid_ShouldPass()
    {
        var validator = new TypeValidator();
        var result = validator.Validate("BigValue", 9223372036854775807L, null, "BigInt");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_BigInt_Invalid_ShouldFail()
    {
        var validator = new TypeValidator();
        var result = validator.Validate("BigValue", "not-a-number", null, "BigInt");

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("expects a BigInt value");
    }

    [Fact]
    public void Validate_Decimal_Valid_ShouldPass()
    {
        var validator = new TypeValidator();
        var result = validator.Validate("Price", 19.99m, null, "Decimal");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_Decimal_Invalid_ShouldFail()
    {
        var validator = new TypeValidator();
        var result = validator.Validate("Price", "not-a-number", null, "Decimal");

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("expects a Decimal value");
    }

    [Fact]
    public void Validate_Boolean_Bool_ShouldPass()
    {
        var validator = new TypeValidator();
        var result = validator.Validate("Active", true, null, "Boolean");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_Boolean_StringTrue_ShouldPass()
    {
        var validator = new TypeValidator();
        var result = validator.Validate("Active", "true", null, "Boolean");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_Boolean_Invalid_ShouldFail()
    {
        var validator = new TypeValidator();
        var result = validator.Validate("Active", "maybe", null, "Boolean");

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("expects a Yes/No");
    }

    [Fact]
    public void Validate_Uuid_ValidGuid_ShouldPass()
    {
        var validator = new TypeValidator();
        var result = validator.Validate("Id", Guid.NewGuid().ToString(), null, "Uuid");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_Uuid_Invalid_ShouldFail()
    {
        var validator = new TypeValidator();
        var result = validator.Validate("Id", "not-a-guid", null, "Uuid");

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("expects a Uuid");
    }

    [Fact]
    public void Validate_UnsupportedType_ShouldFail()
    {
        var validator = new TypeValidator();
        var result = validator.Validate("Col", "value", null, "CustomType");

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("Unsupported base type");
    }
}
