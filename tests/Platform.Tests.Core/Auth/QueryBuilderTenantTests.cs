using System.Collections.Generic;
using Platform.Core.Auth;
using Platform.Core.Metadata;
using Platform.Core.Runtime;
using Platform.Tests.Core.Runtime;

public class QueryBuilderTenantTests
{
    private MockMetadataGraph CreateMockGraph()
    {
        var mock = new MockMetadataGraph();

        // Define a sample table "Customer" with active columns
        mock.AddColumn(new MetaColumn
        {
            ColumnName = "CustomerId", IsActive = true, IsUpdateable = false, IsKey = true,
            TableName = "Customer", BaseType = "Int"
        });
        mock.AddColumn(new MetaColumn
        {
            ColumnName = "Name", IsActive = true, IsUpdateable = true,
            TableName = "Customer", BaseType = "VarChar"
        });
        mock.AddColumn(new MetaColumn
        {
            ColumnName = "Email", IsActive = true, IsUpdateable = true,
            TableName = "Customer", BaseType = "VarChar"
        });
        mock.AddColumn(new MetaColumn
        {
            ColumnName = "SysClient_ID", IsActive = true, IsUpdateable = false,
            TableName = "Customer", BaseType = "Int"
        });
        mock.AddColumn(new MetaColumn
        {
            ColumnName = "SysOrg_ID", IsActive = true, IsUpdateable = false,
            TableName = "Customer", BaseType = "Int"
        });
        mock.AddColumn(new MetaColumn
        {
            ColumnName = "Deleted", IsActive = false, IsUpdateable = false,
            TableName = "Customer", BaseType = "Bool"
        });

        mock.AddTable(new TableMetadata
        {
            SysTableId = 1,
            TableName = "Customer",
            ClassName = "X_Customer"
        });

        return mock;
    }

    [Fact]
    public void ValidateTable_ValidTable_ReturnsQuotedName()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);

        var result = qb.ValidateTable("Customer");

        Assert.Equal("\"Customer\"", result);
    }

    [Fact]
    public void ValidateTable_InvalidTable_ReturnsNull()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);

        var result = qb.ValidateTable("NonExistent");

        Assert.Null(result);
    }

    [Fact]
    public void ValidateColumn_ValidColumn_ReturnsQuotedName()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);

        var result = qb.ValidateColumn("Customer", "Name");

        Assert.Equal("\"Name\"", result);
    }

    [Fact]
    public void ValidateColumn_InvalidColumn_ReturnsNull()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);

        var result = qb.ValidateColumn("Customer", "InvalidColumn");

        Assert.Null(result);
    }

    [Fact]
    public void ValidateSort_ValidColumn_ReturnsQuotedAndDirection()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);

        var result = qb.ValidateSort("Customer", "Name", "desc");

        Assert.NotNull(result);
        Assert.Equal("\"Name\"", result.Value.Column);
        Assert.Equal("DESC", result.Value.Direction);
    }

    [Fact]
    public void ValidateSort_DefaultDirectionIsAsc()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);

        var result = qb.ValidateSort("Customer", "Name", null);

        Assert.NotNull(result);
        Assert.Equal("ASC", result.Value.Direction);
    }

    [Fact]
    public void BuildSelect_ThrowsOnUnknownTable()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.Create(null, null, null);

        Assert.Throws<ArgumentException>(() => qb.BuildSelect("UnknownTable", ctx));
    }

    [Fact]
    public void BuildSelect_InvalidPageSize_Throws()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.Create(null, null, null);

        Assert.Throws<ArgumentOutOfRangeException>(() => qb.BuildSelect("Customer", ctx, page: 1, pageSize: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => qb.BuildSelect("Customer", ctx, page: 1, pageSize: 600));
    }

    [Fact]
    public void BuildSelect_IncludesTenantPredicate_WhenPresent()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.CreateWithTenantIsolation(
            "user1", "100", "200",
            "SysClient_ID = @ClientId",
            "SysOrg_ID = @OrgId");

        var (sql, paramsObj, countSql) = qb.BuildSelect("Customer", ctx);

        var allParams = (Npgsql.NpgsqlParameter[])paramsObj;
        Assert.Contains(allParams, p => p.ParameterName == "@ClientId");
        Assert.Contains(allParams, p => p.ParameterName == "@OrgId");
        Assert.Contains("SysClient_ID", sql);
        Assert.Contains("SysOrg_ID", sql);
    }

    [Fact]
    public void BuildSelect_BuildsValidSql()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.Create(null, null, null);

        var (sql, _, countSql) = qb.BuildSelect("Customer", ctx);

        Assert.StartsWith("SELECT ", sql);
        Assert.Contains("\"Customer\"", sql);
        Assert.Contains("OFFSET", sql);
        Assert.Contains("FETCH NEXT", sql);
        Assert.StartsWith("SELECT COUNT(*) FROM", countSql);
    }

    [Fact]
    public void BuildInsert_BuildsValidSql()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.Create(null, null, null);
        var data = new Dictionary<string, object?>
        {
            { "Name", "Test Corp" },
            { "Email", "test@example.com" }
        };

        var (sql, parameters) = qb.BuildInsert("Customer", data, ctx);

        Assert.StartsWith("INSERT INTO", sql);
        Assert.Contains("\"Customer\"", sql);
        Assert.Contains("\"Name\"", sql);
        Assert.Contains("\"Email\"", sql);
        Assert.Equal(2, parameters.Length);
        Assert.Equal("@p0", parameters[0].ParameterName);
        Assert.Equal("@p1", parameters[1].ParameterName);
    }

    [Fact]
    public void BuildInsert_ExcludesNonWritableColumns()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.Create(null, null, null);
        var data = new Dictionary<string, object?>
        {
            { "Name", "Test Corp" },
            { "CustomerId", 999 },
            { "SysClient_ID", 100 }
        };

        var (sql, parameters) = qb.BuildInsert("Customer", data, ctx);

        Assert.DoesNotContain("CustomerId", sql);
        Assert.DoesNotContain("SysClient_ID", sql);
        Assert.Contains("Name", sql);
    }

    [Fact]
    public void BuildInsert_ThrowsOnNoWritableColumns()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.Create(null, null, null);
        var data = new Dictionary<string, object?>
        {
            { "CustomerId", 999 }
        };

        Assert.Throws<ArgumentException>(() => qb.BuildInsert("Customer", data, ctx));
    }

    [Fact]
    public void BuildUpdate_BuildsValidSql()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.Create(null, null, null);
        var data = new Dictionary<string, object?>
        {
            { "Name", "Updated Corp" }
        };

        var (sql, parameters) = qb.BuildUpdate("Customer", "CustomerId", data, ctx);

        Assert.StartsWith("UPDATE", sql);
        Assert.Contains("\"Customer\"", sql);
        Assert.Contains("SET", sql);
        Assert.Contains("\"Name\" = @p0", sql);
        Assert.Contains("WHERE \"CustomerId\" = @Id", sql);
    }

    [Fact]
    public void BuildDelete_BuildsValidSql()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.Create(null, null, null);

        var (sql, _) = qb.BuildDelete("Customer", "CustomerId", ctx);

        Assert.StartsWith("DELETE FROM", sql);
        Assert.Contains("\"Customer\"", sql);
        Assert.Contains("\"CustomerId\" = @Id", sql);
    }

    [Fact]
    public void BuildDelete_TenantPredicate_IncludedInParameters()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.CreateWithTenantIsolation(
            "user1", "100", "200",
            "\"SysClient_ID\" = @ClientId",
            "\"SysOrg_ID\" = @OrgId");

        var (sql, parameters) = qb.BuildDelete("Customer", "CustomerId", ctx);

        Assert.Contains("@ClientId", sql);
        Assert.Contains("@OrgId", sql);
        Assert.Contains("\"SysClient_ID\"", sql);
        Assert.Contains("\"SysOrg_ID\"", sql);
        var allParams = (Npgsql.NpgsqlParameter[])parameters;
        Assert.Contains(allParams, p => p.ParameterName == "@ClientId");
        Assert.Contains(allParams, p => p.ParameterName == "@OrgId");
    }

    [Fact]
    public void BuildDelete_TenantPredicate_Absent_WhenNoTenantIsolation()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.Create(null, null, null);

        var (sql, parameters) = qb.BuildDelete("Customer", "CustomerId", ctx);

        var allParams = (Npgsql.NpgsqlParameter[])parameters;
        Assert.DoesNotContain(allParams, p => p.ParameterName == "@ClientId");
        Assert.DoesNotContain(allParams, p => p.ParameterName == "@OrgId");
        Assert.DoesNotContain(sql, "@ClientId");
        Assert.DoesNotContain(sql, "@OrgId");
    }

    [Fact]
    public void BuildUpdate_TenantPredicate_IncludedInParameters()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.CreateWithTenantIsolation(
            "user1", "100", "200",
            "\"SysClient_ID\" = @ClientId",
            "\"SysOrg_ID\" = @OrgId");
        var data = new Dictionary<string, object?>
        {
            { "Name", "Updated Corp" }
        };

        var (sql, parameters) = qb.BuildUpdate("Customer", "CustomerId", data, ctx);

        Assert.Contains("UPDATE", sql);
        Assert.Contains("\"Customer\"", sql);
        Assert.Contains("@ClientId", sql);
        Assert.Contains("@OrgId", sql);
        Assert.Contains("\"SysClient_ID\"", sql);
        Assert.Contains("\"SysOrg_ID\"", sql);
        var allParams = (Npgsql.NpgsqlParameter[])parameters;
        Assert.Contains(allParams, p => p.ParameterName == "@ClientId");
        Assert.Contains(allParams, p => p.ParameterName == "@OrgId");
    }

    [Fact]
    public void BuildUpdate_TenantPredicate_Absent_WhenNoTenantIsolation()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.Create(null, null, null);
        var data = new Dictionary<string, object?>
        {
            { "Name", "Updated Corp" }
        };

        var (sql, parameters) = qb.BuildUpdate("Customer", "CustomerId", data, ctx);

        var allParams = (Npgsql.NpgsqlParameter[])parameters;
        Assert.DoesNotContain(allParams, p => p.ParameterName == "@ClientId");
        Assert.DoesNotContain(allParams, p => p.ParameterName == "@OrgId");
        Assert.DoesNotContain(sql, "@ClientId");
        Assert.DoesNotContain(sql, "@OrgId");
    }

    [Fact]
    public void ValidateColumns_MixedValidInvalid_ReturnsNull()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);

        var result = qb.ValidateColumns("Customer", new[] { "Name", "InvalidCol" });

        Assert.Null(result);
    }

    [Fact]
    public void ValidateColumns_AllValid_ReturnsQuoted()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);

        var result = qb.ValidateColumns("Customer", new[] { "Name", "Email" });

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("\"Name\"", result[0]);
        Assert.Equal("\"Email\"", result[1]);
    }

    [Fact]
    public void BuildSelect_WithFilterParams_PassesParameters()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.Create(null, null, null);

        var filterParams = new[] { new Npgsql.NpgsqlParameter("@Name", "Test") };
        var (sql, paramsObj, _) = qb.BuildSelect(
            "Customer", ctx, filterSql: "\"Name\" LIKE @Name", filterParams: filterParams);

        var allParams = (Npgsql.NpgsqlParameter[])paramsObj;
        Assert.Contains(allParams, p => p.ParameterName == "@Name" && p.Value?.ToString() == "Test");
    }

    // ─── DELETE regression tests — parameter propagation through full path ───

    [Fact]
    public void BuildDelete_ContainsIdParameter()
    {
        // Verifies BuildDelete includes @Id as a placeholder parameter (same pattern as BuildUpdate).
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.Create(null, null, null);

        var (sql, parameters) = qb.BuildDelete("Customer", "CustomerId", ctx);

        var allParams = (Npgsql.NpgsqlParameter[])parameters;
        Assert.Contains("@Id", sql);
        Assert.Contains(allParams, p => p.ParameterName == "@Id");
    }

    [Fact]
    public void BuildDelete_TenantPredicate_WithClientIdParameter()
    {
        // Verifies tenant predicate AND @ClientId are both present.
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.CreateWithTenantIsolation(
            "user1", "100", "200",
            "\"SysClient_ID\" = @ClientId",
            "\"SysOrg_ID\" = @OrgId");

        var (sql, parameters) = qb.BuildDelete("Customer", "CustomerId", ctx);

        var allParams = (Npgsql.NpgsqlParameter[])parameters;
        Assert.Contains("\"SysClient_ID\"", sql);
        Assert.Contains(allParams, p => p.ParameterName == "@ClientId" && p.Value?.ToString() == "100");
    }

    [Fact]
    public void BuildDelete_OrgPredicate_WithOrgIdParameter()
    {
        // Verifies org predicate AND @OrgId are both present.
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.CreateWithTenantIsolation(
            "user1", "100", "200",
            "\"SysClient_ID\" = @ClientId",
            "\"SysOrg_ID\" = @OrgId");

        var (sql, parameters) = qb.BuildDelete("Customer", "CustomerId", ctx);

        var allParams = (Npgsql.NpgsqlParameter[])parameters;
        Assert.Contains("\"SysOrg_ID\"", sql);
        Assert.Contains(allParams, p => p.ParameterName == "@OrgId" && p.Value?.ToString() == "200");
    }

    [Fact]
    public void BuildDelete_AllParametersPresent()
    {
        // Regression: BuildDelete must return @Id, @ClientId, @OrgId together.
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.CreateWithTenantIsolation(
            "user1", "100", "200",
            "\"SysClient_ID\" = @ClientId",
            "\"SysOrg_ID\" = @OrgId");

        var (_, parameters) = qb.BuildDelete("Customer", "CustomerId", ctx);

        var allParams = (Npgsql.NpgsqlParameter[])parameters;
        Assert.Contains(allParams, p => p.ParameterName == "@Id");
        Assert.Contains(allParams, p => p.ParameterName == "@ClientId");
        Assert.Contains(allParams, p => p.ParameterName == "@OrgId");
    }

    // ─── UPDATE regression tests — parameter propagation verification ───

    [Fact]
    public void BuildUpdate_ContainsIdParameter()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.Create(null, null, null);
        var data = new Dictionary<string, object?> { { "Name", "Test" } };

        var (sql, parameters) = qb.BuildUpdate("Customer", "CustomerId", data, ctx);

        var allParams = (Npgsql.NpgsqlParameter[])parameters;
        Assert.Contains("@Id", sql);
        Assert.Contains(allParams, p => p.ParameterName == "@Id");
    }

    [Fact]
    public void BuildUpdate_ContainsAllParameterTypes()
    {
        var graph = CreateMockGraph();
        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.CreateWithTenantIsolation(
            "user1", "100", "200",
            "\"SysClient_ID\" = @ClientId",
            "\"SysOrg_ID\" = @OrgId");
        var data = new Dictionary<string, object?> { { "Name", "Test" } };

        var (sql, parameters) = qb.BuildUpdate("Customer", "CustomerId", data, ctx);

        var allParams = (Npgsql.NpgsqlParameter[])parameters;
        Assert.Contains(allParams, p => p.ParameterName == "@Id");
        Assert.Contains(allParams, p => p.ParameterName == "@ClientId");
        Assert.Contains(allParams, p => p.ParameterName == "@OrgId");
        Assert.Contains("\"SysClient_ID\"", sql);
        Assert.Contains("\"SysOrg_ID\"", sql);
    }
}
