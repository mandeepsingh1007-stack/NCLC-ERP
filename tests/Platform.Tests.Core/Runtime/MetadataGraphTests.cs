using FluentAssertions;
using Platform.Core.Metadata;
using Platform.Core.Runtime;

namespace Platform.Tests.Core.Runtime;

/// <summary>
/// MetadataGraph requires a PostgreSQL connection to construct.
/// These tests use the mock where possible, and verify that the real
/// MetadataGraph throws when no DB is available.
/// </summary>
public class MetadataGraphTests
{
    [Fact]
    public void Constructor_ThrowsWithoutDatabase()
    {
        var act = () => new MetadataGraph("Host=localhost;Database=test;Username=test;Password=test");
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void GetColumn_FromMock_ReturnsColumn()
    {
        var mock = new MockMetadataGraph();
        mock.AddColumn(new MetaColumn
        {
            TableName = "Users",
            ColumnName = "UserName",
            Label = "User Name",
            BaseType = "VarChar",
            IsActive = true
        });

        var col = mock.GetColumn("Users", "UserName");

        col.Should().NotBeNull();
        col!.ColumnName.Should().Be("UserName");
    }

    [Fact]
    public void GetTable_FromMock_ReturnsTable()
    {
        var mock = new MockMetadataGraph();
        mock.AddTable(new TableMetadata { SysTableId = 1, TableName = "Users", ClassName = "X_Users" });

        var table = mock.GetTable("Users");

        table.Should().NotBeNull();
        table!.TableName.Should().Be("Users");
    }

    [Fact]
    public void GetTableById_FromMock_ReturnsTable()
    {
        var mock = new MockMetadataGraph();
        mock.AddTable(new TableMetadata { SysTableId = 42, TableName = "Orders", ClassName = "X_Orders" });

        var table = mock.GetTableById(42);

        table.Should().NotBeNull();
        table!.TableName.Should().Be("Orders");
    }

    [Fact]
    public void GetTableById_FromMock_ReturnsNullForUnknownId()
    {
        var mock = new MockMetadataGraph();

        var table = mock.GetTableById(99999);

        table.Should().BeNull();
    }

    [Fact]
    public void GetColumns_FromMock_ReturnsFilteredColumns()
    {
        var mock = new MockMetadataGraph();
        mock.AddColumn(new MetaColumn { TableName = "Users", ColumnName = "UserName", IsActive = true });
        mock.AddColumn(new MetaColumn { TableName = "Users", ColumnName = "Email", IsActive = true });
        mock.AddColumn(new MetaColumn { TableName = "Orders", ColumnName = "OrderId", IsActive = true });

        var columns = mock.GetColumns("Users");

        columns.Count.Should().Be(2);
        columns.Should().Contain(c => c.ColumnName == "UserName");
        columns.Should().Contain(c => c.ColumnName == "Email");
    }

    [Fact]
    public void GetColumns_FromMock_ReturnsEmptyForUnknownTable()
    {
        var mock = new MockMetadataGraph();

        var columns = mock.GetColumns("NonExistent");

        columns.Should().BeEmpty();
    }

    [Fact]
    public void GetReferences_FromMock_ReturnsFilteredReferences()
    {
        var mock = new MockMetadataGraph();
        mock.AddReference(new SysReference { Name = "StatusRef" });
        mock.AddReference(new SysReference { Name = "OtherRef" });

        var refs = mock.GetReferences("StatusRef");

        refs.Count.Should().Be(1);
        refs[0].Name.Should().Be("StatusRef");
    }
}
