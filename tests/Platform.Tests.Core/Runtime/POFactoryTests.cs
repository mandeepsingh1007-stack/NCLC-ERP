using FluentAssertions;
using Platform.Core.Runtime;
using Platform.Metadata.Factory;

namespace Platform.Tests.Core.Runtime;

public class POFactoryTests
{
    [Fact]
    public void Constructor_CreatesFactory()
    {
        var mockGraph = new MockMetadataGraph();
        var factory = new POFactory(mockGraph);

        factory.Should().NotBeNull();
    }

    [Fact]
    public void ResolveMClass_UnknownTable_ReturnsNull()
    {
        var mockGraph = new MockMetadataGraph();
        var factory = new POFactory(mockGraph);

        var result = factory.ResolveMClass("nonexistent_table_xyz");

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveMClass_InvalidTableName_ReturnsNull()
    {
        var mockGraph = new MockMetadataGraph();
        var factory = new POFactory(mockGraph);

        var result = factory.ResolveMClass("table;DROP TABLE");

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveMClass_EmptyTableName_ReturnsNull()
    {
        var mockGraph = new MockMetadataGraph();
        var factory = new POFactory(mockGraph);

        var result = factory.ResolveMClass("");

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveXClass_UnknownTable_ReturnsNull()
    {
        var mockGraph = new MockMetadataGraph();
        var factory = new POFactory(mockGraph);

        var result = factory.ResolveXClass("nonexistent_table_xyz");

        result.Should().BeNull();
    }

    [Fact]
    public void CreateInstance_UnknownTable_ReturnsNull()
    {
        var mockGraph = new MockMetadataGraph();
        var factory = new POFactory(mockGraph);

        var result = factory.CreateInstance("nonexistent_table_xyz");

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveMClass_SpecialCharacters_ReturnsNull()
    {
        var mockGraph = new MockMetadataGraph();
        var factory = new POFactory(mockGraph);

        var result = factory.ResolveMClass("../../../etc/passwd");

        result.Should().BeNull();
    }

    [Fact]
    public void Dispose_CallsDispose()
    {
        var mockGraph = new MockMetadataGraph();
        var factory = new POFactory(mockGraph);

        var act = () => factory.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void ResolveMClass_ValidNameButNoClass_ReturnsNull()
    {
        var mockGraph = new MockMetadataGraph();
        var factory = new POFactory(mockGraph);

        // "Users" is a valid table name but no M_Users class exists
        var result = factory.ResolveMClass("Users");

        result.Should().BeNull();
    }
}
