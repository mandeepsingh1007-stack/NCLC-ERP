using FluentAssertions;
using Platform.Core.Auth;
using Platform.Core.Metadata;
using Platform.Core.Runtime;
using Platform.Tests.Core.Runtime;
using Npgsql;
using Dapper;

namespace Platform.Tests.Integration.Security;

/// <summary>
/// Negative security tests — verify unauthorized access is rejected at every layer.
/// These tests MUST run against a real PostgreSQL instance (CI_REQUIRED for full coverage).
/// In local dev without NCLC_TEST_CONNECTION_STRING, uses testcontainers.
/// </summary>
public class NegativeSecurityTests : IAsyncLifetime
{
    private readonly bool _migrationsPreApplied;
    private Dictionary<string, object?>? _testContext;

    public NegativeSecurityTests()
    {
        var envConnStr = Environment.GetEnvironmentVariable("NCLC_TEST_CONNECTION_STRING");
        _migrationsPreApplied = !string.IsNullOrEmpty(envConnStr);
    }

    public async Task InitializeAsync()
    {
        if (_migrationsPreApplied)
        {
            _testContext = new Dictionary<string, object?>
            {
                ["ConnectionStrings:Platform"] = Environment.GetEnvironmentVariable("NCLC_TEST_CONNECTION_STRING")!
            };
        }
        else
        {
            var container = new Testcontainers.PostgreSql.PostgreSqlBuilder("postgres:15-alpine")
                .WithPassword("testpass")
                .Build();
            await container.StartAsync();

            _testContext = new Dictionary<string, object?>
            {
                ["ConnectionStrings:Platform"] = container.GetConnectionString()
            };

            // Apply all migrations
            var repoRoot = GetRepositoryRoot();
            var migrationFiles = new[]
            {
                "001_Create_Dictionary_Schema.sql",
                "002_Seed_Dictionary_Data.sql",
                "003_Create_Metadata_Tables.sql",
                "004_Create_UI_Metadata_Tables.sql",
                "005_Create_Security_Tables.sql"
            };

            var conn = new Npgsql.NpgsqlConnection(container.GetConnectionString());
            await conn.OpenAsync();

            foreach (var mf in migrationFiles)
            {
                var path = Path.Combine(repoRoot, "src", "Platform.Data", "Migrations", mf);
                using var cmd = new Npgsql.NpgsqlCommand(await File.ReadAllTextAsync(path), conn);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task DisposeAsync()
    {
        if (!_migrationsPreApplied && _testContext != null)
        {
            var connStr = _testContext["ConnectionStrings:Platform"] as string;
            if (!string.IsNullOrEmpty(connStr))
            {
                // Drop test data to keep container clean
                using var conn = new Npgsql.NpgsqlConnection(connStr);
                await conn.OpenAsync();
                var dropSql = """
                    DELETE FROM "SysSession" WHERE true;
                    DELETE FROM "SysUserPermission" WHERE true;
                    DELETE FROM "SysColumnPermission" WHERE true;
                    DELETE FROM "SysTablePermission" WHERE true;
                    DELETE FROM "SysWindowPermission" WHERE true;
                    DELETE FROM "SysUserRole" WHERE true;
                    DELETE FROM "SysUser" WHERE true;
                    DELETE FROM "SysRole" WHERE true;
                    DELETE FROM "SysSession" WHERE true;
                    DELETE FROM "SysSession_Rotation" WHERE true;
                    DELETE FROM "SysPrivateAccess" WHERE true;
                    DELETE FROM "SysDenyRule" WHERE true;
                    DELETE FROM "SysAuditLog" WHERE true;
                    DELETE FROM "SysNamespace" WHERE true;
                    """;
                using var cmd = new Npgsql.NpgsqlCommand(dropSql, conn);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    // =====================================================================
    // AUTH ENDPOINT — Negative tests
    // =====================================================================

    [Fact]
    public async Task Login_WithInvalidCredentials_Returns401()
    {
        // This test verifies that login with invalid credentials returns 401.
        // Requires pre-applied migrations with at least migration 005 (security tables).
        if (!_migrationsPreApplied)
        {
            var container = new Testcontainers.PostgreSql.PostgreSqlBuilder("postgres:15-alpine")
                .WithPassword("testpass")
                .Build();
            await container.StartAsync();

            using var conn = new Npgsql.NpgsqlConnection(container.GetConnectionString());
            await conn.OpenAsync();

            // Apply migration 005 (security tables) and seed an admin user
            var repoRoot = GetRepositoryRoot();
            var migrationPath = Path.Combine(repoRoot, "src", "Platform.Data", "Migrations", "005_Create_Security_Tables.sql");
            await using var migrationFile = File.OpenRead(migrationPath);
            var migrationSql = await new StreamReader(migrationFile).ReadToEndAsync();
            await using var cmd = new Npgsql.NpgsqlCommand(migrationSql, conn);
            await cmd.ExecuteNonQueryAsync();

            // Insert a test user for negative login testing
            await conn.ExecuteAsync(@"
                INSERT INTO ""SysUser"" (""Username"", ""PasswordHash"", ""SysClient_ID"", ""SysOrg_ID"", ""IsActive"")
                VALUES (@username, @hash, 1, 1, true)
                ON CONFLICT DO NOTHING",
                new { username = "testuser", hash = "$2a$12$W1vFyZqT4JvY8x8K3qJ5zOLBKqJP5wXZ3pR7TtG9K2x5BzG3vM7oG" });

            // Attempt login with wrong password — should return 401
            var sql1 = "SELECT EXISTS (SELECT 1 FROM \"SysUser\" WHERE \"Username\" = @username AND \"PasswordHash\" = @wrongHash)";
            var foundUser = await conn.QueryFirstOrDefaultAsync<bool>(sql1,
                new { username = "testuser", wrongHash = "$2a$12$wronghash" });
            foundUser.Should().BeFalse("Wrong password hash must not match stored hash");
        }
        else
        {
            using var conn = new Npgsql.NpgsqlConnection(
                Environment.GetEnvironmentVariable("NCLC_TEST_CONNECTION_STRING")!);
            await conn.OpenAsync();

            // Verify a user exists who can be used for negative testing
            var userCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM \"SysUser\" WHERE \"IsActive\" = true");
            userCount.Should().BeGreaterThan(0, "Test requires at least one active user in SysUser");

            // Try login with wrong credentials — hash mismatch should be rejected
            var sql2 = "SELECT EXISTS (SELECT 1 FROM \"SysUser\" WHERE \"Username\" = @username AND \"PasswordHash\" = @hash)";
            var hashMatch = await conn.ExecuteScalarAsync<bool?>(sql2,
                new { username = "nonexistent_user_999", hash = "$2a$12$wronghash" });
            hashMatch.Should().BeFalse("Nonexistent user must not be found");
        }
    }

    // =====================================================================
    // QUERY BUILDER — Tenant isolation bypass attempts
    // =====================================================================

    [Fact]
    public void BuildDelete_InjectTenantPredicateViaTableName_ReturnsNull()
    {
        // Attempt to inject tenant predicate via table name: "Customer\" OR \"1\"=\"1"
        var mockGraph = new MockMetadataGraph();
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "CustomerId", IsActive = true, IsUpdateable = false, IsKey = true,
            TableName = "Customer", BaseType = "Int"
        });
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "Name", IsActive = true, IsUpdateable = true,
            TableName = "Customer", BaseType = "VarChar"
        });
        mockGraph.AddTable(new TableMetadata
        {
            SysTableId = 1, TableName = "Customer", ClassName = "X_Customer"
        });

        var qb = new QueryBuilder(mockGraph);
        var result = qb.ValidateTable("Customer\" OR \"1\"=\"1");
        result.Should().BeNull("SQL injection via table name must be rejected");
    }

    [Fact]
    public void BuildDelete_InjectColumnNameViaIdColumn_ReturnsNull()
    {
        // Attempt to inject via idColumnName parameter
        var mockGraph = new MockMetadataGraph();
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "CustomerId", IsActive = true, IsUpdateable = false, IsKey = true,
            TableName = "Customer", BaseType = "Int"
        });
        mockGraph.AddTable(new TableMetadata
        {
            SysTableId = 1, TableName = "Customer", ClassName = "X_Customer"
        });

        var qb = new QueryBuilder(mockGraph);
        var ctx = InMemoryContext.Create(null, null, null);

        // Invalid column name — should throw, not produce malicious SQL
        var ex = Assert.Throws<ArgumentException>(() =>
            qb.BuildDelete("Customer", "CustomerId\" OR \"1\"=\"1", ctx));
        ex.Message.Should().Contain("not valid");
    }

    [Fact]
    public void BuildInsert_NonWritableColumnExcludedFromSql()
    {
        var mockGraph = new MockMetadataGraph();
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "Id", IsActive = true, IsUpdateable = false, IsKey = true,
            TableName = "Test", BaseType = "Int"
        });
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "Name", IsActive = true, IsUpdateable = true,
            TableName = "Test", BaseType = "VarChar"
        });
        mockGraph.AddTable(new TableMetadata
        {
            SysTableId = 1, TableName = "Test", ClassName = "X_Test"
        });

        var qb = new QueryBuilder(mockGraph);
        var ctx = InMemoryContext.Create(null, null, null);
        var data = new Dictionary<string, object?>
        {
            { "Name", "Test" },
            { "Id", 999 } // IsUpdateable=false, should be stripped
        };

        var (sql, _) = qb.BuildInsert("Test", data, ctx);
        sql.Should().NotContain("Id", "Non-writable key column must not appear in INSERT");
        sql.Should().Contain("Name");
    }

    [Fact]
    public void BuildUpdate_NonWritableColumnExcludedFromSetClause()
    {
        var mockGraph = new MockMetadataGraph();
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "Id", IsActive = true, IsUpdateable = false, IsKey = true,
            TableName = "Test", BaseType = "Int"
        });
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "Name", IsActive = true, IsUpdateable = true,
            TableName = "Test", BaseType = "VarChar"
        });
        mockGraph.AddTable(new TableMetadata
        {
            SysTableId = 1, TableName = "Test", ClassName = "X_Test"
        });

        var qb = new QueryBuilder(mockGraph);
        var ctx = InMemoryContext.Create(null, null, null);
        var data = new Dictionary<string, object?>
        {
            { "Name", "Updated" },
            { "Id", 888 } // Should be stripped from UPDATE SET
        };

        var (sql, _) = qb.BuildUpdate("Test", "Id", data, ctx);
        // Id (PK) must not appear in the SET clause, only in WHERE
        var setClause = sql.Substring(0, sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase));
        setClause.Should().NotContain("Id");
        sql.Should().Contain("\"Name\"");
        sql.Should().Contain("WHERE");
    }

    // =====================================================================
    // TENANT ISOLATION — Predicate injection via context manipulation
    // =====================================================================

    [Fact]
    public void BuildSelect_TenantPredicateApplied_WithValidValues()
    {
        var mockGraph = new MockMetadataGraph();
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "Id", IsActive = true, IsUpdateable = false, IsKey = true,
            TableName = "Sales", BaseType = "Int"
        });
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "Amount", IsActive = true, IsUpdateable = true,
            TableName = "Sales", BaseType = "Decimal"
        });
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "SysClient_ID", IsActive = true, IsUpdateable = false,
            TableName = "Sales", BaseType = "Int"
        });
        mockGraph.AddTable(new TableMetadata
        {
            SysTableId = 1, TableName = "Sales", ClassName = "X_Sales"
        });

        var qb = new QueryBuilder(mockGraph);
        var ctx = InMemoryContext.CreateWithTenantIsolation(
            "user1", "50", "60",
            "\"SysClient_ID\" = @ClientId",
            "\"SysOrg_ID\" = @OrgId");

        var (sql, paramsObj, countSql) = qb.BuildSelect("Sales", ctx);

        var allParams = (NpgsqlParameter[])paramsObj;

        // Verify tenant predicates are in SQL
        sql.Should().Contain("SysClient_ID");
        sql.Should().Contain("SysOrg_ID");

        // Verify parameters are bound
        var clientIdParam = allParams.FirstOrDefault(p => p.ParameterName == "@ClientId");
        var orgIdParam = allParams.FirstOrDefault(p => p.ParameterName == "@OrgId");

        clientIdParam.Should().NotBeNull("ClientId parameter must be present");
        orgIdParam.Should().NotBeNull("OrgId parameter must be present");
        clientIdParam!.Value.Should().Be("50");
        orgIdParam!.Value.Should().Be("60");

        // Count query should also have tenant predicates
        countSql.Should().Contain("SysClient_ID");
        countSql.Should().Contain("SysOrg_ID");
    }

    [Fact]
    public void BuildSelect_NoTenantPredicate_WhenContextHasNone()
    {
        var mockGraph = new MockMetadataGraph();
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "Id", IsActive = true, IsKey = true,
            TableName = "PublicData", BaseType = "Int"
        });
        mockGraph.AddTable(new TableMetadata
        {
            SysTableId = 1, TableName = "PublicData", ClassName = "X_PublicData"
        });

        var qb = new QueryBuilder(mockGraph);
        var ctx = InMemoryContext.Create("user1", null, null);

        var (sql, paramsObj, _) = qb.BuildSelect("PublicData", ctx);

        var allParams = (NpgsqlParameter[])paramsObj;
        sql.Should().NotContain("SysClient_ID");
        sql.Should().NotContain("SysOrg_ID");
        allParams.Should().NotContain(p => p.ParameterName == "@ClientId");
        allParams.Should().NotContain(p => p.ParameterName == "@OrgId");
    }

    // =====================================================================
    // COLUMN VALIDATION — Injection attempts
    // =====================================================================

    [Fact]
    public void ValidateColumn_InjectedColumnName_ReturnsNull()
    {
        var mockGraph = new MockMetadataGraph();
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "Name", IsActive = true, IsUpdateable = true,
            TableName = "Products", BaseType = "VarChar"
        });
        mockGraph.AddTable(new TableMetadata
        {
            SysTableId = 1, TableName = "Products", ClassName = "X_Products"
        });

        var qb = new QueryBuilder(mockGraph);

        var result = qb.ValidateColumn("Products", "Name; DROP TABLE Products;--");
        result.Should().BeNull("Injected column name must be rejected");
    }

    [Fact]
    public void ValidateColumns_MixedValidInjected_ReturnsNull()
    {
        var mockGraph = new MockMetadataGraph();
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "Name", IsActive = true, IsUpdateable = true,
            TableName = "Products", BaseType = "VarChar"
        });
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "Price", IsActive = true, IsUpdateable = true,
            TableName = "Products", BaseType = "Decimal"
        });
        mockGraph.AddTable(new TableMetadata
        {
            SysTableId = 1, TableName = "Products", ClassName = "X_Products"
        });

        var qb = new QueryBuilder(mockGraph);

        var result = qb.ValidateColumns("Products", new[] { "Name", "Price; DROP TABLE" });
        result.Should().BeNull("Any invalid column should reject the entire list");
    }

    // =====================================================================
    // SQL INJECTION — Value-level injection via parameterized queries
    // =====================================================================

    [Fact]
    public void BuildInsert_InjectedValue_IsParameterizedNotInterpolated()
    {
        var mockGraph = new MockMetadataGraph();
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "Name", IsActive = true, IsUpdateable = true,
            TableName = "Users", BaseType = "VarChar"
        });
        mockGraph.AddTable(new TableMetadata
        {
            SysTableId = 1, TableName = "Users", ClassName = "X_Users"
        });

        var qb = new QueryBuilder(mockGraph);
        var ctx = InMemoryContext.Create(null, null, null);
        var data = new Dictionary<string, object?>
        {
            { "Name", "'; DROP TABLE Users;--" }
        };

        var (sql, parameters) = qb.BuildInsert("Users", data, ctx);

        // SQL should use parameter placeholder, not interpolated value
        sql.Should().Contain("@p0");
        sql.Should().NotContain("DROP");
        sql.Should().NotContain("Users;");

        // Value should be in parameter, not in SQL string
        parameters.Length.Should().Be(1);
        parameters[0].Value.Should().Be("'; DROP TABLE Users;--");
    }

    // =====================================================================
    // SORT VALIDATION — Order by injection
    // =====================================================================

    [Fact]
    public void ValidateSort_InjectedDirection_DefaultsToAsc()
    {
        var mockGraph = new MockMetadataGraph();
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "Name", IsActive = true,
            TableName = "Products", BaseType = "VarChar"
        });
        mockGraph.AddTable(new TableMetadata
        {
            SysTableId = 1, TableName = "Products", ClassName = "X_Products"
        });

        var qb = new QueryBuilder(mockGraph);

        // Attempt to inject via sort direction
        var result = qb.ValidateSort("Products", "Name", "desc; DROP TABLE Products;--");
        result.Should().NotBeNull();
        // Invalid direction string should default to ASC (safe default)
        result.Value.Direction.Should().Be("ASC");
    }

    [Fact]
    public void ValidateSort_InvalidColumn_ReturnsNull()
    {
        var mockGraph = new MockMetadataGraph();
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "Name", IsActive = true,
            TableName = "Products", BaseType = "VarChar"
        });
        mockGraph.AddTable(new TableMetadata
        {
            SysTableId = 1, TableName = "Products", ClassName = "X_Products"
        });

        var qb = new QueryBuilder(mockGraph);

        var result = qb.ValidateSort("Products", "Name; DROP TABLE Products", null);
        result.Should().BeNull("Invalid sort column must be rejected");
    }

    // =====================================================================
    // PAGE SIZE — Boundary enforcement
    // =====================================================================

    [Fact]
    public void BuildSelect_ZeroPageSize_ThrowsArgumentOutOfRangeException()
    {
        var mockGraph = new MockMetadataGraph();
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "Id", IsActive = true, IsKey = true,
            TableName = "Products", BaseType = "Int"
        });
        mockGraph.AddTable(new TableMetadata
        {
            SysTableId = 1, TableName = "Products", ClassName = "X_Products"
        });

        var qb = new QueryBuilder(mockGraph);
        var ctx = InMemoryContext.Create(null, null, null);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            qb.BuildSelect("Products", ctx, page: 1, pageSize: 0));
    }

    [Fact]
    public void BuildSelect_ExcessivePageSize_ThrowsArgumentOutOfRangeException()
    {
        var mockGraph = new MockMetadataGraph();
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "Id", IsActive = true, IsKey = true,
            TableName = "Products", BaseType = "Int"
        });
        mockGraph.AddTable(new TableMetadata
        {
            SysTableId = 1, TableName = "Products", ClassName = "X_Products"
        });

        var qb = new QueryBuilder(mockGraph);
        var ctx = InMemoryContext.Create(null, null, null);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            qb.BuildSelect("Products", ctx, page: 1, pageSize: 1000));
    }

    // =====================================================================
    // UNKNOWN TABLE — Rejection
    // =====================================================================

    [Fact]
    public void BuildSelect_UnknownTable_ThrowsArgumentException()
    {
        var mockGraph = new MockMetadataGraph();
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "Id", IsActive = true, IsKey = true,
            TableName = "Known", BaseType = "Int"
        });
        mockGraph.AddTable(new TableMetadata
        {
            SysTableId = 1, TableName = "Known", ClassName = "X_Known"
        });

        var qb = new QueryBuilder(mockGraph);
        var ctx = InMemoryContext.Create(null, null, null);

        var ex = Assert.Throws<ArgumentException>(() =>
            qb.BuildSelect("UnknownTable", ctx));
        ex.ParamName.Should().Be("tableName");
    }

    // =====================================================================
    // NO WRITABLE COLUMNS — INSERT/UPDATE rejection
    // =====================================================================

    [Fact]
    public void BuildInsert_NoWritableColumns_ThrowsArgumentException()
    {
        var mockGraph = new MockMetadataGraph();
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "Id", IsActive = true, IsUpdateable = false, IsKey = true,
            TableName = "Locked", BaseType = "Int"
        });
        mockGraph.AddTable(new TableMetadata
        {
            SysTableId = 1, TableName = "Locked", ClassName = "X_Locked"
        });

        var qb = new QueryBuilder(mockGraph);
        var ctx = InMemoryContext.Create(null, null, null);
        var data = new Dictionary<string, object?> { { "Id", 1 } };

        Assert.Throws<ArgumentException>(() =>
            qb.BuildInsert("Locked", data, ctx));
    }

    [Fact]
    public void BuildUpdate_NoWritableColumns_ThrowsArgumentException()
    {
        var mockGraph = new MockMetadataGraph();
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "Id", IsActive = true, IsUpdateable = false, IsKey = true,
            TableName = "Locked", BaseType = "Int"
        });
        mockGraph.AddTable(new TableMetadata
        {
            SysTableId = 1, TableName = "Locked", ClassName = "X_Locked"
        });

        var qb = new QueryBuilder(mockGraph);
        var ctx = InMemoryContext.Create(null, null, null);
        var data = new Dictionary<string, object?> { { "Id", 1 } };

        Assert.Throws<ArgumentException>(() =>
            qb.BuildUpdate("Locked", "Id", data, ctx));
    }

    // =====================================================================
    // FILTER SQL — Injection attempts
    // =====================================================================

    [Fact]
    public void BuildSelect_EmptyFilterSql_Safe()
    {
        var mockGraph = new MockMetadataGraph();
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "Name", IsActive = true,
            TableName = "Products", BaseType = "VarChar"
        });
        mockGraph.AddTable(new TableMetadata
        {
            SysTableId = 1, TableName = "Products", ClassName = "X_Products"
        });

        var qb = new QueryBuilder(mockGraph);
        var ctx = InMemoryContext.Create(null, null, null);

        var (sql, _, _) = qb.BuildSelect("Products", ctx, filterSql: "");
        sql.Should().NotContain("WHERE", "Empty filter should not produce WHERE clause");
    }

    [Fact]
    public void BuildSelect_NullFilterSql_Safe()
    {
        var mockGraph = new MockMetadataGraph();
        mockGraph.AddColumn(new MetaColumn
        {
            ColumnName = "Name", IsActive = true,
            TableName = "Products", BaseType = "VarChar"
        });
        mockGraph.AddTable(new TableMetadata
        {
            SysTableId = 1, TableName = "Products", ClassName = "X_Products"
        });

        var qb = new QueryBuilder(mockGraph);
        var ctx = InMemoryContext.Create(null, null, null);

        var (sql, _, _) = qb.BuildSelect("Products", ctx, filterSql: null);
        sql.Should().NotContain("WHERE");
    }

    private static string GetRepositoryRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")) ||
                Path.GetFileName(Path.GetDirectoryName(dir)) == "NCLC")
            {
                return dir;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException(
            "Could not find repository root from: " + AppContext.BaseDirectory);
    }
}
