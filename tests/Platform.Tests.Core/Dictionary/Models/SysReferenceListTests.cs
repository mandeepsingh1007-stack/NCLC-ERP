using FluentAssertions;
using Platform.Core.Metadata;

namespace Platform.Tests.Core.Dictionary.Models;

public class SysReferenceListTests
{
    [Fact]
    public void Create_ValidData_ShouldHaveCorrectValues()
    {
        var sut = new SysReferenceList
        {
            SysReferenceListId = 1,
            SysReferenceId = 1,
            Value = "Yes",
            Name = "Yes",
            SeqNo = 1,
            IsActive = true,
        };

        sut.SysReferenceListId.Should().Be(1);
        sut.SysReferenceId.Should().Be(1);
        sut.Value.Should().Be("Yes");
        sut.Name.Should().Be("Yes");
        sut.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_DefaultValues_ShouldHaveIsActiveTrue()
    {
        var sut = new SysReferenceList();
        sut.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_DefaultValues_ShouldHaveSeqNo0()
    {
        var sut = new SysReferenceList();
        sut.SeqNo.Should().Be(0);
    }
}
