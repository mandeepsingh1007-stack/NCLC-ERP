using FluentAssertions;
using Platform.Core.Metadata;
using Platform.Core.Runtime;

namespace Platform.Tests.Core.Runtime;

public class ContextVariableResolverTests
{
    [Fact]
    public void GetCurrentContext_ReturnsDefaultContext()
    {
        var resolver = new ContextVariableResolver();
        var context = resolver.GetCurrentContext();

        context.Should().NotBeNull();
        context.UserId.Should().BeNull();
        context.TenantId.Should().BeNull();
        context.OrgId.Should().BeNull();
        context.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Resolve_Userid_ReturnsFromContext()
    {
        var resolver = new ContextVariableResolver();
        var context = InMemoryContext.Create("user123", "tenant1", "org1");

        var result = resolver.Resolve<string>("$UserId", context);

        result.Should().Be("user123");
    }

    [Fact]
    public void Resolve_Tenantid_ReturnsFromContext()
    {
        var resolver = new ContextVariableResolver();
        var context = InMemoryContext.Create("user123", "tenant1", "org1");

        var result = resolver.Resolve<string>("$TenantId", context);

        result.Should().Be("tenant1");
    }

    [Fact]
    public void Resolve_Orgid_ReturnsFromContext()
    {
        var resolver = new ContextVariableResolver();
        var context = InMemoryContext.Create("user123", "tenant1", "org1");

        var result = resolver.Resolve<string>("$OrgId", context);

        result.Should().Be("org1");
    }

    [Fact]
    public void Resolve_Timestamp_ReturnsContextTimestamp()
    {
        var resolver = new ContextVariableResolver();
        var timestamp = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var context = new TestContext(timestamp);

        var result = resolver.Resolve<DateTime>("$Timestamp", context);

        result.Should().Be(timestamp);
    }

    [Fact]
    public void Resolve_UnknownVariable_ReturnsNull()
    {
        var resolver = new ContextVariableResolver();
        var context = InMemoryContext.Create("user123", "tenant1", "org1");

        var result = resolver.Resolve<string>("$UnknownVar", context);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_EmptyExpression_ReturnsDefault()
    {
        var resolver = new ContextVariableResolver();
        var context = InMemoryContext.Create("user123", "tenant1", "org1");

        var result = resolver.Resolve<string>("", context);

        result.Should().BeNull();
    }

    // Simple test context with custom timestamp for testing
    private sealed class TestContext : Platform.Core.Metadata.IReadOnlyContext
    {
        private readonly DateTime _timestamp;

        public TestContext(DateTime timestamp) => _timestamp = timestamp;
        public string? UserId => null;
        public string? TenantId => null;
        public string? OrgId => null;
        public DateTime Timestamp => _timestamp;
        public object? Value => null;
        public object? ExistingValue => null;
        public IReadOnlyDictionary<string, object?> Extensions => new Dictionary<string, object?>();
        public string? TenantPredicate => null;
        public string? OrgPredicate => null;
    }
}
