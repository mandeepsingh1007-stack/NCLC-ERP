namespace Platform.Tests.Core;

public class TestBase
{
    private readonly string _testId = Guid.NewGuid().ToString("N");

    protected string TestId => _testId;

    /// <summary>
    /// Generates a unique tenant name scoped to this test run.
    /// </summary>
    protected string TenantName => $"test_tenant_{_testId}";

    /// <summary>
    /// Generates a unique organization name scoped to this test run.
    /// </summary>
    protected string OrgName => $"test_org_{_testId}";
}
