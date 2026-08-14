using FluentAssertions;

namespace Platform.Tests.Core;

public class PlatformTests : TestBase
{
    [Fact]
    public void TestId_GeneratesUniqueGuid()
    {
        this.TestId.Should().NotBeEmpty();
        this.TestId.Should().HaveLength(32); // hex representation of Guid
    }

    [Fact]
    public void TestId_DifferentForEachInstance()
    {
        var one = new PlatformTests();
        var two = new PlatformTests();
        one.TestId.Should().NotBe(two.TestId);
    }
}
