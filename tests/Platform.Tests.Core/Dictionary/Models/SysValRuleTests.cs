using FluentAssertions;
using Platform.Core.Metadata;

namespace Platform.Tests.Core.Dictionary.Models;

public class SysValRuleTests
{
    [Fact]
    public void Create_ValidData_ShouldHaveCorrectValues()
    {
        var sut = new SysValRule
        {
            SysValRuleId = 1,
            Name = "NotNull",
            Description = "Value must not be null or empty",
            RuleType = ValRuleTypeEnum.Sql,
            Code = "VALUE IS NOT NULL",
            IsActive = true,
        };

        sut.SysValRuleId.Should().Be(1);
        sut.Name.Should().Be("NotNull");
        sut.RuleType.Should().Be(ValRuleTypeEnum.Sql);
        sut.Code.Should().Be("VALUE IS NOT NULL");
        sut.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_DefaultValues_ShouldHaveActiveTrue()
    {
        var sut = new SysValRule();
        sut.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_AllRuleTypes_ShouldHaveExpectedValues()
    {
        ((int)ValRuleTypeEnum.Sql).Should().Be(1);
        ((int)ValRuleTypeEnum.Regex).Should().Be(2);
        ((int)ValRuleTypeEnum.Lambda).Should().Be(3);
        ((int)ValRuleTypeEnum.Script).Should().Be(4);
    }
}
