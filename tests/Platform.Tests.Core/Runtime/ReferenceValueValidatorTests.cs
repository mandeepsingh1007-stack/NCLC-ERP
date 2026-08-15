using FluentAssertions;
using Platform.Core.Metadata;
using Platform.Core.Runtime;

namespace Platform.Tests.Core.Runtime;

public class ReferenceValueValidatorTests
{
    [Fact]
    public void Validate_NullValue_ShouldPass()
    {
        var mockGraph = new MockMetadataGraph();
        var validator = new ReferenceValueValidator(mockGraph);
        var result = validator.Validate("Status", null, 1, "LIST", "TestTable", null);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyStringValue_ShouldPass()
    {
        var mockGraph = new MockMetadataGraph();
        var validator = new ReferenceValueValidator(mockGraph);
        var result = validator.Validate("Status", "", 1, "LIST", "TestTable", null);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_Search_ShouldAlwaysPass()
    {
        var mockGraph = new MockMetadataGraph();
        var validator = new ReferenceValueValidator(mockGraph);
        var result = validator.Validate("SearchColumn", "anything", 1, "SEARCH", "TestTable", null);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_Table_EmptyValue_ShouldFail()
    {
        var mockGraph = new MockMetadataGraph();
        var validator = new ReferenceValueValidator(mockGraph);
        var result = validator.Validate("FKColumn", "", 1, "TABLE", "TestTable", null);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("cannot be empty"));
    }

    [Fact]
    public void Validate_Table_NullValue_ShouldFail()
    {
        // TABLE rejects null values — FK must have a value
        var mockGraph = new MockMetadataGraph();
        var validator = new ReferenceValueValidator(mockGraph);
        var result = validator.Validate("FKColumn", null, 1, "TABLE", "TestTable", null);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("cannot be empty"));
    }

    [Fact]
    public void Validate_Table_ValidValue_ShouldPass()
    {
        // Phase 2: non-empty string passes; full FK check is deferred to Phase 3
        var mockGraph = new MockMetadataGraph();
        var validator = new ReferenceValueValidator(mockGraph);
        var result = validator.Validate("FKColumn", "some-value", 1, "TABLE", "TestTable", null);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_NoValidationType_ShouldPass()
    {
        var mockGraph = new MockMetadataGraph();
        var validator = new ReferenceValueValidator(mockGraph);
        var result = validator.Validate("Column", "value", null, null, "TestTable", null);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_List_WithSeedListValues_ShouldPassValidValue()
    {
        var mockGraph = new MockMetadataGraph();
        var sysRef = new SysReference { Name = "StatusRef", SysReferenceId = 1 };
        mockGraph.AddReference(sysRef);
        mockGraph.AddReferenceList(new SysReferenceList { SysReferenceId = sysRef.SysReferenceId, Value = "Active" });
        mockGraph.AddReferenceList(new SysReferenceList { SysReferenceId = sysRef.SysReferenceId, Value = "Inactive" });

        var validator = new ReferenceValueValidator(mockGraph);
        var result = validator.Validate("Status", "Active", sysRef.SysReferenceId, "LIST", "TestTable", "StatusRef");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_List_InvalidValue_ShouldFail()
    {
        var mockGraph = new MockMetadataGraph();
        var sysRef = new SysReference { Name = "StatusRef", SysReferenceId = 2 };
        mockGraph.AddReference(sysRef);
        mockGraph.AddReferenceList(new SysReferenceList { SysReferenceId = sysRef.SysReferenceId, Value = "Active" });

        var validator = new ReferenceValueValidator(mockGraph);
        var result = validator.Validate("Status", "INVALID", sysRef.SysReferenceId, "LIST", "TestTable", "StatusRef");

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not in the allowed list"));
    }

    [Fact]
    public void Validate_List_NoListValuesLoaded_ShouldPass()
    {
        // If no list values are loaded, validation passes through (deferred)
        var mockGraph = new MockMetadataGraph();
        mockGraph.AddReference(new SysReference { Name = "EmptyRef", SysReferenceId = 3 });

        var validator = new ReferenceValueValidator(mockGraph);
        var result = validator.Validate("Status", "anything", 1, "LIST", "TestTable", null);

        result.IsSuccess.Should().BeTrue();
    }
}
