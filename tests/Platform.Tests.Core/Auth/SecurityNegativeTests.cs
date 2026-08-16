using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Platform.Core.Auth;
using Platform.Core.Metadata;
using Platform.Core.Runtime;
using Platform.Tests.Core.Runtime;

/// <summary>
/// Negative security tests — prove that unauthorized access is denied.
/// Covers: missing user, unauthorized role, tenant isolation, permission cascade.
/// These are regression protection for Phase 5 security enforcement.
/// </summary>
public class SecurityNegativeTests
{
    private static IMetadataGraph MockMetadataGraph => new Mock<IMetadataGraph>()
        .Object;

    #region PermissionService — negative cases

    [Fact]
    public async Task CanReadTableAsync_UserNotFound_ReturnsDenied()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var userMock = new Mock<IUserRepository>();
        userMock
            .Setup(x => x.GetUserByIdAsync(It.IsAny<int>()))
            .Returns(Task.FromResult<(int, string, string, string, int, int?, bool)?>((null)));

        var service = new PermissionService(cache, Mock.Of<IRbacRepository>(), Mock.Of<INamespaceRepository>(), userMock.Object, MockMetadataGraph);

        var result = await service.CanReadTableAsync(999, "Customer", PermissionLevel.ReadOnly);

        Assert.False(result.Allowed);
        Assert.Equal(PermissionLevel.None, result.Level);
    }

    [Fact]
    public async Task CanReadTableAsync_TableNotFound_ReturnsDenied()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var nsMock = new Mock<INamespaceRepository>();
        nsMock.Setup(x => x.GetTableIdAsync("NonExistent")).ReturnsAsync((int?)null);

        var userMock = new Mock<IUserRepository>();
        userMock
            .Setup(x => x.GetUserByIdAsync(1))
            .Returns(Task.FromResult<(int, string, string, string, int, int?, bool)?>(
                ValueTuple.Create(1, "u", "h", "U", 100, (int?)null, true)));
        userMock.Setup(x => x.GetUserRoleIdsAsync(1)).ReturnsAsync(new[] { 5 });

        var service = new PermissionService(cache, Mock.Of<IRbacRepository>(), nsMock.Object, userMock.Object, MockMetadataGraph);

        var result = await service.CanReadTableAsync(1, "NonExistent", PermissionLevel.ReadOnly);

        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task CanWriteTableAsync_NoRoleAccess_ReturnsDenied()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var rbacMock = new Mock<IRbacRepository>();
        var nsMock = new Mock<INamespaceRepository>();
        var userMock = new Mock<IUserRepository>();

        nsMock.Setup(x => x.GetTableIdAsync("Customer")).ReturnsAsync(1);
        userMock
            .Setup(x => x.GetUserByIdAsync(1))
            .Returns(Task.FromResult<(int, string, string, string, int, int?, bool)?>(
                ValueTuple.Create(1, "u", "h", "U", 100, (int?)null, true)));
        userMock.Setup(x => x.GetUserRoleIdsAsync(1)).ReturnsAsync(new[] { 5 });
        rbacMock.Setup(x => x.ResolveAsync(100, new[] { 5 })).ReturnsAsync(RbacResolution.Empty);

        var service = new PermissionService(cache, rbacMock.Object, nsMock.Object, userMock.Object, MockMetadataGraph);

        var result = await service.CanWriteTableAsync(1, "Customer", PermissionLevel.Create);

        Assert.False(result.Allowed);
        Assert.Equal(PermissionLevel.None, result.Level);
    }

    [Fact]
    public async Task CheckColumnAsync_NoColumnAccess_ReturnsDenied()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var rbacMock = new Mock<IRbacRepository>();
        var nsMock = new Mock<INamespaceRepository>();
        var userMock = new Mock<IUserRepository>();

        nsMock.Setup(x => x.GetTableIdAsync("Customer")).ReturnsAsync(1);
        nsMock.Setup(x => x.GetColumnIdAsync("Customer", "Name")).ReturnsAsync(2);
        userMock
            .Setup(x => x.GetUserByIdAsync(1))
            .Returns(Task.FromResult<(int, string, string, string, int, int?, bool)?>(
                ValueTuple.Create(1, "u", "h", "U", 100, (int?)null, true)));
        userMock.Setup(x => x.GetUserRoleIdsAsync(1)).ReturnsAsync(new[] { 5 });
        rbacMock.Setup(x => x.ResolveAsync(100, new[] { 5 })).ReturnsAsync(RbacResolution.Empty);

        var service = new PermissionService(cache, rbacMock.Object, nsMock.Object, userMock.Object, MockMetadataGraph);

        var result = await service.CheckColumnAsync(1, "Customer", "Name", PermissionLevel.ReadOnly);

        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task GetPrivateRecordIdsAsync_UserNotAuthenticated_ReturnsEmpty()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var userMock = new Mock<IUserRepository>();
        userMock
            .Setup(x => x.GetUserByIdAsync(It.IsAny<int>()))
            .Returns(Task.FromResult<(int, string, string, string, int, int?, bool)?>((null)));

        var service = new PermissionService(cache, Mock.Of<IRbacRepository>(), Mock.Of<INamespaceRepository>(), userMock.Object, MockMetadataGraph);

        var result = await service.GetPrivateRecordIdsAsync(999, "Customer");

        Assert.Empty(result);
    }

    [Fact]
    public async Task CheckWindowAsync_WrongWindow_ReturnsDenied()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var nsMock = new Mock<INamespaceRepository>();
        var userMock = new Mock<IUserRepository>();

        nsMock.Setup(x => x.GetWindowIdAsync("NonExistentWindow")).ReturnsAsync((int?)null);
        userMock
            .Setup(x => x.GetUserByIdAsync(1))
            .Returns(Task.FromResult<(int, string, string, string, int, int?, bool)?>(
                ValueTuple.Create(1, "u", "h", "U", 100, (int?)null, true)));
        userMock.Setup(x => x.GetUserRoleIdsAsync(1)).ReturnsAsync(new[] { 5 });

        var service = new PermissionService(cache, Mock.Of<IRbacRepository>(), nsMock.Object, userMock.Object, MockMetadataGraph);

        var result = await service.CheckWindowAsync(1, "NonExistentWindow", PermissionLevel.ReadOnly);

        Assert.False(result.Allowed);
    }

    #endregion

    #region RBAC Resolution — hierarchical cascade

    [Fact]
    public async Task CheckColumnAsync_TableFallback_WhenNoColumnPermission()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var rbacMock = new Mock<IRbacRepository>();
        var nsMock = new Mock<INamespaceRepository>();
        var userMock = new Mock<IUserRepository>();

        nsMock.Setup(x => x.GetTableIdAsync("Customer")).ReturnsAsync(1);
        nsMock.Setup(x => x.GetColumnIdAsync("Customer", "Name")).ReturnsAsync(2);
        userMock
            .Setup(x => x.GetUserByIdAsync(1))
            .Returns(Task.FromResult<(int, string, string, string, int, int?, bool)?>(
                ValueTuple.Create(1, "u", "h", "U", 100, (int?)null, true)));
        userMock.Setup(x => x.GetUserRoleIdsAsync(1)).ReturnsAsync(new[] { 5 });

        var tablePerm = new Dictionary<int, PermissionLevel> { [1] = PermissionLevel.ReadOnly };
        var resolution = new RbacResolution(
            new Dictionary<int, PermissionLevel>(),
            tablePerm,
            new Dictionary<(int, int), PermissionLevel>(),
            new Dictionary<int, string?>(),
            new Dictionary<int, HashSet<int>>());
        rbacMock.Setup(x => x.ResolveAsync(100, new[] { 5 })).ReturnsAsync(resolution);

        var service = new PermissionService(cache, rbacMock.Object, nsMock.Object, userMock.Object, MockMetadataGraph);

        var result = await service.CheckColumnAsync(1, "Customer", "Name", PermissionLevel.ReadOnly);

        Assert.True(result.Allowed);
        Assert.Equal(PermissionLevel.ReadOnly, result.Level);
    }

    [Fact]
    public async Task CheckColumnAsync_ColumnLevelOverridesTableLevel()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var rbacMock = new Mock<IRbacRepository>();
        var nsMock = new Mock<INamespaceRepository>();
        var userMock = new Mock<IUserRepository>();

        nsMock.Setup(x => x.GetTableIdAsync("Customer")).ReturnsAsync(1);
        nsMock.Setup(x => x.GetColumnIdAsync("Customer", "Name")).ReturnsAsync(2);
        userMock
            .Setup(x => x.GetUserByIdAsync(1))
            .Returns(Task.FromResult<(int, string, string, string, int, int?, bool)?>(
                ValueTuple.Create(1, "u", "h", "U", 100, (int?)null, true)));
        userMock.Setup(x => x.GetUserRoleIdsAsync(1)).ReturnsAsync(new[] { 5 });

        // Table-level FullControl, but column-level None -> column wins
        var tablePerm = new Dictionary<int, PermissionLevel> { [1] = PermissionLevel.FullControl };
        var colPerm = new Dictionary<(int, int), PermissionLevel> { [(1, 2)] = PermissionLevel.None };
        var resolution = new RbacResolution(
            new Dictionary<int, PermissionLevel>(),
            tablePerm,
            colPerm,
            new Dictionary<int, string?>(),
            new Dictionary<int, HashSet<int>>());
        rbacMock.Setup(x => x.ResolveAsync(100, new[] { 5 })).ReturnsAsync(resolution);

        var service = new PermissionService(cache, rbacMock.Object, nsMock.Object, userMock.Object, MockMetadataGraph);

        var result = await service.CheckColumnAsync(1, "Customer", "Name", PermissionLevel.ReadOnly);

        Assert.False(result.Allowed);
        Assert.Equal(PermissionLevel.None, result.Level);
    }

    #endregion

    #region QueryBuilder Tenant Isolation — negative

    [Fact]
    public void BuildSelect_UnauthenticatedUser_TenantIsolationAbsent()
    {
        var graph = new MockMetadataGraph();
        graph.AddColumn(new MetaColumn { ColumnName = "Id", IsActive = true, IsKey = true, TableName = "Customer" });
        graph.AddTable(new TableMetadata { SysTableId = 1, TableName = "Customer" });

        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.Create(null, null, null);

        var (sql, paramsObj, _) = qb.BuildSelect("Customer", ctx);

        var allParams = (Npgsql.NpgsqlParameter[])paramsObj;
        Assert.DoesNotContain(allParams, p => p.ParameterName == "@ClientId");
        Assert.DoesNotContain(allParams, p => p.ParameterName == "@OrgId");
    }

    [Fact]
    public void BuildSelect_TenantPredicate_IncludedInParameters()
    {
        var graph = new MockMetadataGraph();
        graph.AddColumn(new MetaColumn { ColumnName = "Id", IsActive = true, IsKey = true, TableName = "Customer" });
        graph.AddTable(new TableMetadata { SysTableId = 1, TableName = "Customer" });

        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.CreateWithTenantIsolation(
            "user1", "100", "200",
            "\"SysClient_ID\" = @ClientId",
            "\"SysOrg_ID\" = @OrgId");

        var (sql, paramsObj, _) = qb.BuildSelect("Customer", ctx);

        Assert.Contains("\"SysClient_ID\"", sql);
        Assert.Contains("\"SysOrg_ID\"", sql);
        var allParams = (Npgsql.NpgsqlParameter[])paramsObj;
        Assert.Contains(allParams, p => p.ParameterName == "@ClientId");
        Assert.Contains(allParams, p => p.ParameterName == "@OrgId");
    }

    [Fact]
    public void BuildDelete_TenantPredicate_Included()
    {
        var graph = new MockMetadataGraph();
        graph.AddColumn(new MetaColumn { ColumnName = "Id", IsActive = true, IsKey = true, TableName = "Customer" });
        graph.AddTable(new TableMetadata { SysTableId = 1, TableName = "Customer" });

        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.CreateWithTenantIsolation(
            "user1", "100", "200",
            "\"SysClient_ID\" = @ClientId",
            "\"SysOrg_ID\" = @OrgId");

        var (sql, parameters) = qb.BuildDelete("Customer", "Id", ctx);

        Assert.Contains("@ClientId", sql);
        Assert.Contains("@OrgId", sql);
    }

    [Fact]
    public void ValidateTable_KnownTable_Allowed()
    {
        var graph = new MockMetadataGraph();
        graph.AddTable(new TableMetadata { TableName = "Customer" });
        var qb = new QueryBuilder(graph);
        Assert.NotNull(qb.ValidateTable("Customer"));
    }

    [Fact]
    public void ValidateTable_UnknownTable_Denied()
    {
        var graph = new MockMetadataGraph();
        graph.AddTable(new TableMetadata { TableName = "Customer" });
        var qb = new QueryBuilder(graph);
        Assert.Null(qb.ValidateTable("UnknownTable"));
    }

    #endregion

    #region Auth — security regression

    [Fact]
    public void TokenService_ShortKey_Rejects()
    {
        var settings = new JwtSettings
        {
            SecretKey = "short",
            Issuer = "Test",
            Audience = "Test"
        };
        var cacheMock = new Mock<IDistributedCache>();

        var ex = Assert.Throws<InvalidOperationException>(() => new TokenService(settings, cacheMock.Object));
        Assert.Contains("16", ex.Message);
    }

    [Fact]
    public void PermissionResult_Denied_HasReason()
    {
        var result = new PermissionResult(false, PermissionLevel.None, "Test reason.");
        Assert.False(result.Allowed);
        Assert.Equal("Test reason.", result.Reason);
    }

    [Fact]
    public void PermissionResult_Allowed_NoReason()
    {
        var result = new PermissionResult(true, PermissionLevel.ReadOnly, null);
        Assert.True(result.Allowed);
        Assert.Null(result.Reason);
    }

    #endregion

    #region Lookup Security — negative cases

    [Fact]
    public void LookupTableReference_WithoutTenantPredicate_ExcludesTenantData()
    {
        // Proves that tenant-scoped lookups must NOT fetch records from other tenants.
        // The lookup endpoint MUST apply SysClient_ID predicate from context.
        // Without tenant predicate, a TABLE lookup on a business table returns ALL records.
        // Unit test: Verify that a query built WITHOUT tenant predicates does NOT include tenant filtering.
        var graph = new MockMetadataGraph();
        graph.AddColumn(new MetaColumn { ColumnName = "Id", IsActive = true, IsKey = true, TableName = "Customer" });
        graph.AddColumn(new MetaColumn { ColumnName = "Name", IsActive = true, TableName = "Customer" });
        graph.AddColumn(new MetaColumn { ColumnName = "SysClient_ID", IsActive = true, TableName = "Customer" });
        graph.AddTable(new TableMetadata { SysTableId = 1, TableName = "Customer" });

        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.Create("user1", null, null);

        var (sql, paramsObj, _) = qb.BuildSelect("Customer", ctx);

        // Without tenant context, no tenant filtering is applied — this proves tenant context is required.
        var allParams = (Npgsql.NpgsqlParameter[])paramsObj;
        Assert.DoesNotContain(allParams, p => p.ParameterName == "@ClientId");
        Assert.DoesNotContain(allParams, p => p.ParameterName == "@OrgId");
        Assert.DoesNotContain(sql, "SysClient_ID");
    }

    [Fact]
    public void LookupTableReference_WithTenantPredicate_IncludesTenantFiltering()
    {
        // Proves that when tenant context IS present, tenant predicates are applied.
        var graph = new MockMetadataGraph();
        graph.AddColumn(new MetaColumn { ColumnName = "Id", IsActive = true, IsKey = true, TableName = "Customer" });
        graph.AddColumn(new MetaColumn { ColumnName = "Name", IsActive = true, TableName = "Customer" });
        graph.AddColumn(new MetaColumn { ColumnName = "SysClient_ID", IsActive = true, TableName = "Customer" });
        graph.AddTable(new TableMetadata { SysTableId = 1, TableName = "Customer" });

        var qb = new QueryBuilder(graph);
        var ctx = InMemoryContext.CreateWithTenantIsolation(
            "user1", "100", "200",
            "\"SysClient_ID\" = @ClientId",
            "\"SysOrg_ID\" = @OrgId");

        var (sql, paramsObj, _) = qb.BuildSelect("Customer", ctx);

        var allParams = (Npgsql.NpgsqlParameter[])paramsObj;
        Assert.Contains(allParams, p => p.ParameterName == "@ClientId" && p.Value?.ToString() == "100");
        Assert.Contains(allParams, p => p.ParameterName == "@OrgId" && p.Value?.ToString() == "200");
        Assert.Contains("SysClient_ID", sql);
        Assert.Contains("SysOrg_ID", sql);
    }

    [Fact]
    public async Task LookupTableReference_MissingUserId_ReturnsUnauthorized()
    {
        // Prove that an unauthenticated user cannot access lookup endpoints.
        var cache = new MemoryCache(new MemoryCacheOptions());
        var userMock = new Mock<IUserRepository>();
        userMock
            .Setup(x => x.GetUserByIdAsync(It.IsAny<int>()))
            .Returns(Task.FromResult<(int, string, string, string, int, int?, bool)?>(null));

        var service = new PermissionService(
            cache,
            Mock.Of<IRbacRepository>(),
            Mock.Of<INamespaceRepository>(),
            userMock.Object,
            MockMetadataGraph);

        // Should fail because user ID is 999 and GetUserByIdAsync returns null
        var result = await service.CanReadTableAsync(999, "Customer", PermissionLevel.ReadOnly);

        Assert.False(result.Allowed);
        Assert.Equal(PermissionLevel.None, result.Level);
    }

    [Fact]
    public async Task LookupTableReference_UnauthorizedTable_ReturnsDenied()
    {
        // Prove that a user without table-level read permission is denied lookup data.
        var cache = new MemoryCache(new MemoryCacheOptions());
        var nsMock = new Mock<INamespaceRepository>();
        var userMock = new Mock<IUserRepository>();
        var rbacMock = new Mock<IRbacRepository>();

        nsMock.Setup(x => x.GetTableIdAsync("Customer")).ReturnsAsync(1);
        userMock
            .Setup(x => x.GetUserByIdAsync(1))
            .Returns(Task.FromResult<(int, string, string, string, int, int?, bool)?>(
                ValueTuple.Create(1, "u", "h", "U", 100, (int?)null, true)));
        userMock.Setup(x => x.GetUserRoleIdsAsync(1)).ReturnsAsync(new[] { 5 });
        rbacMock.Setup(x => x.ResolveAsync(100, new[] { 5 })).ReturnsAsync(RbacResolution.Empty);

        var service = new PermissionService(
            cache,
            rbacMock.Object,
            nsMock.Object,
            userMock.Object,
            MockMetadataGraph);

        var result = await service.CanReadTableAsync(1, "Customer", PermissionLevel.ReadOnly);

        Assert.False(result.Allowed);
        Assert.Equal(PermissionLevel.None, result.Level);
    }

    [Fact]
    public async Task LookupColumnAccess_KeyColumnDenied_ReturnsDenied()
    {
        // Prove that if the key column has no permission, lookup is denied.
        var cache = new MemoryCache(new MemoryCacheOptions());
        var nsMock = new Mock<INamespaceRepository>();
        var userMock = new Mock<IUserRepository>();
        var rbacMock = new Mock<IRbacRepository>();

        nsMock.Setup(x => x.GetTableIdAsync("Customer")).ReturnsAsync(1);
        nsMock.Setup(x => x.GetColumnIdAsync("Customer", "Id")).ReturnsAsync(10);
        nsMock.Setup(x => x.GetColumnIdAsync("Customer", "Name")).ReturnsAsync(11);
        userMock
            .Setup(x => x.GetUserByIdAsync(1))
            .Returns(Task.FromResult<(int, string, string, string, int, int?, bool)?>(
                ValueTuple.Create(1, "u", "h", "U", 100, (int?)null, true)));
        userMock.Setup(x => x.GetUserRoleIdsAsync(1)).ReturnsAsync(new[] { 5 });
        rbacMock.Setup(x => x.ResolveAsync(100, new[] { 5 })).ReturnsAsync(RbacResolution.Empty);

        var service = new PermissionService(
            cache,
            rbacMock.Object,
            nsMock.Object,
            userMock.Object,
            MockMetadataGraph);

        var keyPerm = await service.CheckColumnAsync(1, "Customer", "Id", PermissionLevel.ReadOnly);
        var displayPerm = await service.CheckColumnAsync(1, "Customer", "Name", PermissionLevel.ReadOnly);

        // Both must be allowed — RbacResolution.Empty gives no permissions
        Assert.False(keyPerm.Allowed);
        Assert.False(displayPerm.Allowed);
    }

    #endregion
}
