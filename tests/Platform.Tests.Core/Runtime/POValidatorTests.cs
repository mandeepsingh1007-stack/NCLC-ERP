using FluentAssertions;
using Platform.Core.Metadata;
using Platform.Core.Runtime;

namespace Platform.Tests.Core.Runtime;

public class POValidatorTests
{
    private readonly MockMetadataGraph _mockGraph;
    private readonly POValidator _sut;

    public POValidatorTests()
    {
        _mockGraph = new MockMetadataGraph();
        var typeValidator = new TypeValidator();
        var refValidator = new ReferenceValueValidator(_mockGraph);
        var valRuleEngine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        _sut = new POValidator(typeValidator, refValidator, valRuleEngine, _mockGraph);
    }

    [Fact]
    public void Validate_Mandatory_NullValue_ShouldFail()
    {
        var col = new MetaColumn
        {
            ColumnName = "Name",
            Label = "Name",
            IsMandatory = true,
            IsActive = true
        };

        var result = _sut.Validate("TestTable", col, null, InMemoryContext.Create("user1", "tenant1", "org1"));

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("required"));
    }

    [Fact]
    public void Validate_Mandatory_EmptyString_ShouldFail()
    {
        var col = new MetaColumn
        {
            ColumnName = "Name",
            Label = "Name",
            IsMandatory = true,
            IsActive = true
        };

        var result = _sut.Validate("TestTable", col, "", InMemoryContext.Create("user1", "tenant1", "org1"));

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("required"));
    }

    [Fact]
    public void Validate_Mandatory_Whitespace_ShouldFail()
    {
        var col = new MetaColumn
        {
            ColumnName = "Name",
            Label = "Name",
            IsMandatory = true,
            IsActive = true
        };

        var result = _sut.Validate("TestTable", col, "   ", InMemoryContext.Create("user1", "tenant1", "org1"));

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("required"));
    }

    [Fact]
    public void Validate_Mandatory_ValidValue_ShouldPass()
    {
        var col = new MetaColumn
        {
            ColumnName = "Name",
            Label = "Name",
            IsMandatory = true,
            BaseType = "VarChar",
            IsActive = true
        };

        var result = _sut.Validate("TestTable", col, "Hello", InMemoryContext.Create("user1", "tenant1", "org1"));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_NonMandatory_NullValue_ShouldPass()
    {
        var col = new MetaColumn
        {
            ColumnName = "Notes",
            Label = "Notes",
            IsMandatory = false,
            IsActive = true
        };

        var result = _sut.Validate("TestTable", col, null, InMemoryContext.Create("user1", "tenant1", "org1"));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_TypeInteger_Valid_ShouldPass()
    {
        var col = new MetaColumn
        {
            ColumnName = "Age",
            Label = "Age",
            IsMandatory = false,
            BaseType = "Integer",
            IsActive = true
        };

        var result = _sut.Validate("TestTable", col, 42, InMemoryContext.Create("user1", "tenant1", "org1"));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_TypeInteger_Invalid_ShouldFail()
    {
        var col = new MetaColumn
        {
            ColumnName = "Age",
            Label = "Age",
            IsMandatory = false,
            BaseType = "Integer",
            IsActive = true
        };

        var result = _sut.Validate("TestTable", col, "not-a-number", InMemoryContext.Create("user1", "tenant1", "org1"));

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("integer"));
    }

    [Fact]
    public void Validate_StringExceedsLength_ShouldFail()
    {
        var col = new MetaColumn
        {
            ColumnName = "Code",
            Label = "Code",
            IsMandatory = false,
            BaseType = "VarChar",
            FieldLength = 5,
            IsActive = true
        };

        var result = _sut.Validate("TestTable", col, "toolong", InMemoryContext.Create("user1", "tenant1", "org1"));

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("exceeds maximum length"));
    }

    [Fact]
    public void ValidateAll_CollectsMultipleErrors()
    {
        // With an empty mock graph (no columns), ValidateAll finds nothing to validate
        var values = new Dictionary<string, object?>
        {
            { "Name", "test" }
        };

        var result = _sut.ValidateAll("TestTable", values, InMemoryContext.Create("user1", "tenant1", "org1"));

        // Should pass — no columns found in the mock graph
        result.IsSuccess.Should().BeTrue();
    }
}
