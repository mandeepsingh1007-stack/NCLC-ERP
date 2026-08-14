using FluentAssertions;
using Platform.Core.Metadata;

namespace Platform.Tests.Core.Dictionary.Models;

public class SysReferenceTests
{
    [Fact]
    public void Create_ValidData_ShouldHaveCorrectValues()
    {
        var sut = new SysReference
        {
            SysReferenceId = 1,
            Name = "List",
            ValidationType = ValidationTypeEnum.List,
            IsSystemType = true,
            ValueFormat = "alpha",
        };

        sut.SysReferenceId.Should().Be(1);
        sut.Name.Should().Be("List");
        sut.ValidationType.Should().Be(ValidationTypeEnum.List);
        sut.IsSystemType.Should().BeTrue();
    }

    [Fact]
    public void Create_DefaultValues_ShouldHaveIsSystemTypeFalse()
    {
        var sut = new SysReference();
        sut.IsSystemType.Should().BeFalse();
    }

    [Fact]
    public void Create_AllValidationTypes_ShouldHaveExpectedValues()
    {
        ((int)ValidationTypeEnum.List).Should().Be(1);
        ((int)ValidationTypeEnum.Table).Should().Be(2);
        ((int)ValidationTypeEnum.Search).Should().Be(3);
    }
}
