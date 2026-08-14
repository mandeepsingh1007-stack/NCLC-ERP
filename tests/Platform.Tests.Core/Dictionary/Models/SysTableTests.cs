using FluentAssertions;
using Platform.Core.Metadata;

namespace Platform.Tests.Core.Dictionary.Models;

public class SysTableTests
{
    [Fact]
    public void Create_ValidData_ShouldHaveCorrectValues()
    {
        var sut = new SysTable
        {
            SysTableId = 1,
            TableName = "Users",
            ClassName = "Users",
            Description = "User accounts table",
            AccessLevel = 3,
            EntityType = "D",
            IsActive = true,
        };

        sut.SysTableId.Should().Be(1);
        sut.TableName.Should().Be("Users");
        sut.AccessLevel.Should().Be(3);
        sut.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_DefaultValues_ShouldHaveAccessLevel3()
    {
        var sut = new SysTable();
        sut.AccessLevel.Should().Be(3);
    }

    [Fact]
    public void Create_DefaultValues_ShouldHaveEntityTypeD()
    {
        var sut = new SysTable();
        sut.EntityType.Should().Be("D");
    }
}
