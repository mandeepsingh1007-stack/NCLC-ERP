using FluentAssertions;
using Platform.Core.Metadata;

namespace Platform.Tests.Core.Dictionary.Models;

public class SysTranslationTests
{
    [Fact]
    public void Create_ValidData_ShouldHaveCorrectValues()
    {
        var sut = new SysTranslation
        {
            SysElementId = 1,
            LanguageCode = "en-US",
            Name = "Test Column",
            Description = "A test column",
        };

        sut.SysElementId.Should().Be(1);
        sut.LanguageCode.Should().Be("en-US");
        sut.Name.Should().Be("Test Column");
    }

    [Fact]
    public void Create_DefaultValues_ShouldHaveEmptyLanguageCode()
    {
        var sut = new SysTranslation();
        sut.LanguageCode.Should().BeEmpty();
    }

    [Fact]
    public void Create_DefaultValues_ShouldHaveNullOptionalFields()
    {
        var sut = new SysTranslation();
        sut.Name.Should().BeNull();
        sut.Description.Should().BeNull();
        sut.Help.Should().BeNull();
    }
}
