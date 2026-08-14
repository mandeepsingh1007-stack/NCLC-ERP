using FluentAssertions;
using Platform.Core.Metadata;

namespace Platform.Tests.Core.Dictionary.Models;

public class SysColumnTests
{
    [Fact]
    public void Create_ValidData_ShouldHaveCorrectValues()
    {
        var sut = new SysColumn
        {
            SysColumnId = 1,
            SysTableId = 1,
            ColumnName = "UserName",
            SysReferenceId = 1,
            IsMandatory = true,
            IsActive = true,
        };

        sut.SysColumnId.Should().Be(1);
        sut.SysTableId.Should().Be(1);
        sut.ColumnName.Should().Be("UserName");
        sut.SysReferenceId.Should().Be(1);
        sut.IsMandatory.Should().BeTrue();
        sut.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_DefaultValues_ShouldHaveSysReferenceIdAsRequired()
    {
        // SysReferenceId is int (not int?) — default is 0, meaning caller must set it
        var sut = new SysColumn();
        // 0 is the CLR default for int — this is expected; DI/repository must set it
        sut.SysReferenceId.Should().Be(0);
    }

    [Fact]
    public void Create_DefaultValues_ShouldHaveIsActiveTrue()
    {
        var sut = new SysColumn();
        sut.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_DefaultValues_ShouldHaveEntityTypeD()
    {
        var sut = new SysColumn();
        sut.EntityType.Should().Be("D");
    }
}
