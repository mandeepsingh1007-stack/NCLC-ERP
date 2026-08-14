using FluentAssertions;
using Platform.Core.Metadata;

namespace Platform.Tests.Core.Dictionary.Models;

public class SysElementTests
{
    [Fact]
    public void Create_ValidData_ShouldHaveCorrectValues()
    {
        var sut = new SysElement
        {
            SysElementId = 1,
            ColumnName = "TestColumn",
            Name = "Test Column",
            Description = "A test column",
            Help = "Help text",
            IsActive = true,
        };

        sut.SysElementId.Should().Be(1);
        sut.ColumnName.Should().Be("TestColumn");
        sut.Name.Should().Be("Test Column");
        sut.Description.Should().Be("A test column");
        sut.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_DefaultValues_ShouldHaveActiveTrue()
    {
        var sut = new SysElement();
        sut.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_StringProperties_ShouldBeEmptyByDefault()
    {
        var sut = new SysElement();
        sut.ColumnName.Should().BeEmpty();
        sut.Name.Should().BeEmpty();
    }
}
