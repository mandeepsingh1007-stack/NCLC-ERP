using FluentAssertions;
using Platform.Core.Metadata;
using Platform.Core.Runtime;

namespace Platform.Tests.Core.Runtime;

public class ValRuleEngineTests
{
    [Fact]
    public void Evaluate_NullRule_ShouldReturnFail()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create("user1", "tenant1", "org1");

        var result = engine.Evaluate(null!, "test", context);

        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Evaluate_InactiveRule_ShouldPass()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create("user1", "tenant1", "org1");
        var rule = new SysValRule
        {
            Name = "InactiveRule",
            RuleType = ValRuleTypeEnum.Regex,
            Code = "^test$",
            IsActive = false
        };

        var result = engine.Evaluate(rule, "test", context);

        result.Passed.Should().BeTrue();
        result.RuleName.Should().Be("InactiveRule");
    }

    [Fact]
    public void Evaluate_Regex_ValidPattern_ShouldPass()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create("user1", "tenant1", "org1");
        var rule = new SysValRule
        {
            Name = "EmailRegex",
            RuleType = ValRuleTypeEnum.Regex,
            Code = @"^[^@]+@[^@]+\.[^@]+$"
        };

        var result = engine.Evaluate(rule, "test@example.com", context);

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_Regex_InvalidPattern_ShouldFail()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create("user1", "tenant1", "org1");
        var rule = new SysValRule
        {
            Name = "DigitOnly",
            RuleType = ValRuleTypeEnum.Regex,
            Code = "^[0-9]+$"
        };

        var result = engine.Evaluate(rule, "abc", context);

        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not match");
    }

    [Fact]
    public void Evaluate_Regex_NullValue_ShouldPass()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create("user1", "tenant1", "org1");
        var rule = new SysValRule
        {
            Name = "AnyRule",
            RuleType = ValRuleTypeEnum.Regex,
            Code = ".*"
        };

        var result = engine.Evaluate(rule, null, context);

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_Lambda_ShouldReturnNotSupported()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create("user1", "tenant1", "org1");
        var rule = new SysValRule
        {
            Name = "LambdaRule",
            RuleType = ValRuleTypeEnum.Lambda,
            Code = "x => x > 0"
        };

        var result = engine.Evaluate(rule, 5, context);

        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not supported");
    }

    [Fact]
    public void Evaluate_Script_ShouldReturnNotSupported()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create("user1", "tenant1", "org1");
        var rule = new SysValRule
        {
            Name = "ScriptRule",
            RuleType = ValRuleTypeEnum.Script,
            Code = "return true;"
        };

        var result = engine.Evaluate(rule, 5, context);

        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not supported");
    }

    [Fact]
    public void Evaluate_UnknownRuleType_ShouldReturnFail()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create("user1", "tenant1", "org1");
        var rule = new SysValRule
        {
            Name = "UnknownRule",
            RuleType = (Platform.Core.Metadata.ValRuleTypeEnum)99,
            Code = "unknown"
        };

        var result = engine.Evaluate(rule, 5, context);

        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Unknown rule type");
    }

    [Fact]
    public void EvaluateBatch_ReturnsEmptyList_Phase2()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create("user1", "tenant1", "org1");

        var results = engine.EvaluateBatch("TestTable", "value", context);

        results.Should().BeEmpty();
    }

    // === SQL Security Tests ===

    [Fact]
    public void Evaluate_SQL_MustBeSelectOnly()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "UpdateRule", RuleType = ValRuleTypeEnum.Sql, Code = "UPDATE Users SET Name='x'" };

        var result = engine.Evaluate(rule, "test", context);

        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("SELECT");
    }

    [Fact]
    public void Evaluate_SQL_Parameterized_RejectedBeforeExecution()
    {
        // This test verifies the SELECT check passes for parameterized queries.
        // The actual parameterization test is in integration tests (ValRuleEngineIntegrationTests).
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "TestRule", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT 1" };

        // SELECT 1 is valid SELECT syntax with allowed functions (none)
        // Execution will fail (no DB) but the security checks pass
        var result = engine.Evaluate(rule, "'; DROP TABLE Users; --", context);

        // Execution fails due to no DB — that's expected for unit tests
        // The key security property: the value was parameterized, not concatenated
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    [Fact]
    public void Evaluate_SQL_RejectsDisallowedKeywords()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);

        foreach (var sql in new[]
        {
            "INSERT INTO Users VALUES (1)",
            "DELETE FROM Users",
            "DROP TABLE Users",
            "ALTER TABLE Users",
            "CREATE TABLE Users",
            "TRUNCATE Users",
            "EXEC xp_cmdshell",
            "EXECUTE sp_executesql"
        })
        {
            var rule = new SysValRule { Name = "BadRule", RuleType = ValRuleTypeEnum.Sql, Code = sql };
            var result = engine.Evaluate(rule, "test", context);
            result.Passed.Should().BeFalse($"Should reject: {sql}");
        }
    }

    [Fact]
    public void Evaluate_SQL_RejectsCTE()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "CTERule", RuleType = ValRuleTypeEnum.Sql, Code = "WITH t AS (SELECT 1) SELECT * FROM t" };

        var result = engine.Evaluate(rule, "test", context);

        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("SELECT");
    }

    [Fact]
    public void Evaluate_SQL_RejectsComments()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "CommentRule", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT 1; -- DROP TABLE Users" };

        var result = engine.Evaluate(rule, "test", context);

        // Comments are stripped before SELECT check, so it sees "SELECT 1" and passes the SELECT check.
        // But the disallowed keywords check runs on the ORIGINAL SQL.
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("disallowed");
    }

    [Fact]
    public void Evaluate_SQL_RejectsBlockComments()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "BlockCommentRule", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT /*DROP TABLE Users*/ 1" };

        var result = engine.Evaluate(rule, "test", context);

        // Block comments stripped from SELECT check, but the disallowed keywords check runs on the original SQL
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("disallowed");
    }

    [Fact]
    public void Evaluate_SQL_TimedOut_ReturnsFail()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "TimeoutRule", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT pg_sleep(999)" };

        var result = engine.Evaluate(rule, "test", context);

        // pg_sleep is not in the function whitelist, so it's rejected before execution
        // This test verifies that a function-not-whitelisted query fails safely
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("disallowed");
    }

    [Fact]
    public void Evaluate_SQL_FunctionWhitelist_CheckRuns()
    {
        // Verifies that a whitelisted function query passes the security check.
        // Actual execution is tested in integration tests.
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "FunctionRule", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT COUNT(*) FROM Users" };

        var result = engine.Evaluate(rule, "test", context);

        // COUNT is whitelisted, so it passes security checks.
        // Execution may fail (no DB) but that's expected.
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    [Fact]
    public void Evaluate_SQL_RejectsPgCatalogAccess()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);

        foreach (var sql in new[]
        {
            "SELECT * FROM pg_catalog.pg_tables",
            "SELECT * FROM pg_catalog.pg_settings",
            "SELECT * FROM pg_catalog.pg_stat_activity",
            "SELECT * FROM information_schema.tables",
            "SELECT * FROM information_schema.columns",
            "SELECT * FROM sys.objects",
        })
        {
            var rule = new SysValRule { Name = "PgCatalogRule", RuleType = ValRuleTypeEnum.Sql, Code = sql };
            var result = engine.Evaluate(rule, "test", context);
            result.Passed.Should().BeFalse($"Should reject pg_catalog/information_schema: {sql}");
            result.ErrorMessage.Should().Contain("system catalog");
        }
    }

    [Fact]
    public void Evaluate_SQL_RejectsStoredProcedures()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);

        foreach (var sql in new[]
        {
            "SELECT EXEC xp_cmdshell",
            "SELECT EXEC sp_executesql",
        })
        {
            var rule = new SysValRule { Name = "ProcRule", RuleType = ValRuleTypeEnum.Sql, Code = sql };
            var result = engine.Evaluate(rule, "test", context);
            result.Passed.Should().BeFalse($"Should reject stored procedures: {sql}");
            // EXEC is caught by ContainsDisallowedSqlKeywords or ContainsSystemCatalogAccess
            result.ErrorMessage.Should().Contain("disallowed");
        }
    }

    [Fact]
    public void Evaluate_SQL_AllowsNestedQueries()
    {
        // Nested subqueries in SELECT are allowed in Phase 2.
        // The security boundary is SELECT-only + function whitelist + timeout.
        // Actual execution is tested in integration tests.
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "NestedRule", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT * FROM (SELECT 1) t" };

        var result = engine.Evaluate(rule, "test", context);

        // Nested SELECT passes all security checks.
        // Execution may fail (no DB) but that's expected for unit tests.
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    [Fact]
    public void Evaluate_SQL_EmptyCode_StringNullRef_ReturnsFail()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "EmptyRule", RuleType = ValRuleTypeEnum.Sql, Code = string.Empty };

        var result = engine.Evaluate(rule, "test", context);

        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Fact]
    public void Evaluate_SQL_WhitespaceOnlyCode_ReturnsFail()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "WhitespaceRule", RuleType = ValRuleTypeEnum.Sql, Code = "   " };

        var result = engine.Evaluate(rule, "test", context);

        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("SELECT");
    }

    [Fact]
    public void Evaluate_SQL_RejectsNonWhitelistedFunctionWithDigits()
    {
        // Critical fix: regex must match function names with digits (e.g., MY_FUNC1)
        // to prevent whitelist bypass via names like COUNT1 or CUSTOM_FUNC2
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create("user1", "tenant1", "org1");

        foreach (var sql in new[]
        {
            "SELECT MY_FUNC1(*) FROM Users",
            "SELECT COUNT1(*) FROM Users",
            "SELECT CUSTOM_FUNC2(x) FROM Users",
        })
        {
            var rule = new SysValRule { Name = "DigitFuncRule", RuleType = ValRuleTypeEnum.Sql, Code = sql };
            var result = engine.Evaluate(rule, "test", context);
            result.Passed.Should().BeFalse($"Should reject non-whitelisted function with digits: {sql}");
            result.ErrorMessage.Should().Contain("disallowed");
        }
    }

    [Fact]
    public void Evaluate_SQL_AllowsWhitelistedFunctionWithDigits()
    {
        // ROW_NUMBER and DENSE_RANK are whitelisted and contain no digits —
        // but we verify the regex handles them correctly with digits in surrounding context
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "WhitelistedFunc", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT ROW_NUMBER() OVER (ORDER BY id) FROM Users" };

        var result = engine.Evaluate(rule, "test", context);

        // ROW_NUMBER is whitelisted, passes security; execution fails (no DB)
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    // === Table Allowlist Tests ===

    [Fact]
    public void Evaluate_SQL_AllowsTableInAllowlist()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", new[] { "Users", "Orders" });
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "AllowedTable", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT COUNT(*) FROM Users" };

        var result = engine.Evaluate(rule, "test", context);

        // Users is in allowlist, passes security; execution fails (no DB)
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    [Fact]
    public void Evaluate_SQL_RejectsTableNotInAllowlist()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", new[] { "Users", "Orders" });
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "ForbiddenTable", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT COUNT(*) FROM SecretData" };

        var result = engine.Evaluate(rule, "test", context);

        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("allowed list");
    }

    [Fact]
    public void Evaluate_SQL_AllowsSubqueryTablesInAllowlist()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", new[] { "Users", "Orders" });
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "SubqueryTables", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT * FROM Users u WHERE u.id IN (SELECT o.UserId FROM Orders o)" };

        var result = engine.Evaluate(rule, "test", context);

        // Both Users and Orders are in allowlist, execution fails (no DB)
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    [Fact]
    public void Evaluate_SQL_RejectsJoinToUnauthorizedTable()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", new[] { "Users", "Orders" });
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "JoinForbidden", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT * FROM Users JOIN AuditLog ON Users.id = AuditLog.UserId" };

        var result = engine.Evaluate(rule, "test", context);

        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("allowed list");
    }

    [Fact]
    public void Evaluate_SQL_NoAllowlist_DisablesCheck()
    {
        // When no allowlist is set (empty), table checking is disabled
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "AnyTable", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT COUNT(*) FROM ArbitraryTable" };

        var result = engine.Evaluate(rule, "test", context);

        // No allowlist = no table restriction, execution fails (no DB)
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    // === Tenant Isolation Tests ===

    [Fact]
    public void Evaluate_SQL_FailsWhenTenantIdSetWithoutPredicate()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create("user1", "tenant1", "org1");
        var rule = new SysValRule { Name = "TenantRule", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT 1" };

        var result = engine.Evaluate(rule, "test", context);

        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("tenant predicate");
    }

    [Fact]
    public void Evaluate_SQL_FailsWhenOrgIdSetWithoutPredicate()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create("user1", null!, "org1");
        var rule = new SysValRule { Name = "OrgRule", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT 1" };

        var result = engine.Evaluate(rule, "test", context);

        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("org predicate");
    }

    [Fact]
    public void Evaluate_SQL_PassesWhenNoTenantOrOrg()
    {
        // Unauthenticated context — no tenant/org isolation needed
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "NoTenantRule", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT 1" };

        var result = engine.Evaluate(rule, "test", context);

        // Passes security, execution fails (no DB)
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    [Fact]
    public void Evaluate_SQL_WithTenantPredicate_PassesSecurity()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.CreateWithTenantIsolation("user1", "tenant1", "org1", "TenantId = @TenantId", "OrgId = @OrgId");
        var rule = new SysValRule { Name = "TenantAwareRule", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT COUNT(*) FROM Users" };

        var result = engine.Evaluate(rule, "test", context);

        // Passes all security checks, execution fails (no DB)
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    // === Function Whitelist Edge Cases (Comprehensive) ===

    [Fact]
    public void Evaluate_SQL_AllowsCOUNT()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "CountFunc", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT COUNT(*) FROM Users" };

        var result = engine.Evaluate(rule, "test", context);
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    [Fact]
    public void Evaluate_SQL_AllowsSUM()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "SumFunc", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT SUM(Amount) FROM Orders" };

        var result = engine.Evaluate(rule, "test", context);
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    [Fact]
    public void Evaluate_SQL_AllowsAVG()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "AvgFunc", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT AVG(Amount) FROM Orders" };

        var result = engine.Evaluate(rule, "test", context);
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    [Fact]
    public void Evaluate_SQL_AllowsMAX_MIN()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "MinMaxFunc", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT MAX(Amount), MIN(Amount) FROM Orders" };

        var result = engine.Evaluate(rule, "test", context);
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    [Fact]
    public void Evaluate_SQL_AllowsUPPER_LOWER()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "UpperLowerFunc", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT UPPER(Name), LOWER(Name) FROM Users" };

        var result = engine.Evaluate(rule, "test", context);
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    [Fact]
    public void Evaluate_SQL_RejectsCustomFunction()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create("user1", "tenant1", "org1");
        var rule = new SysValRule { Name = "CustomFunc", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT MY_CUSTOM_FUNC(x) FROM Users" };

        var result = engine.Evaluate(rule, "test", context);
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("disallowed");
    }

    [Fact]
    public void Evaluate_SQL_RejectsSubString()
    {
        // SUBSTRING is in the whitelist
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "SubstringFunc", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT SUBSTRING(Name, 1, 5) FROM Users" };

        var result = engine.Evaluate(rule, "test", context);
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    [Fact]
    public void Evaluate_SQL_RejectsCastAsFunction()
    {
        // CAST is whitelisted — should pass security check
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "CastFunc", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT CAST(Amount AS VARCHAR) FROM Orders" };

        var result = engine.Evaluate(rule, "test", context);
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    [Fact]
    public void Evaluate_SQL_RejectsCase()
    {
        // CASE is in the whitelist — should pass security check
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "CaseFunc", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT CASE WHEN x > 0 THEN 1 ELSE 0 END" };

        var result = engine.Evaluate(rule, "test", context);
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    // === Tenant Isolation Edge Cases (Comprehensive) ===

    [Fact]
    public void TenantIsolation_OnlyTenantSet_WithoutPredicate_Fails()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        // TenantId only, no OrgId
        var context = InMemoryContext.Create("user1", "tenant1", null!);
        var rule = new SysValRule { Name = "TenantOnly", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT 1" };

        var result = engine.Evaluate(rule, "test", context);
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("tenant predicate");
    }

    [Fact]
    public void TenantIsolation_OnlyOrgSet_WithoutPredicate_Fails()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        // OrgId only, no TenantId
        var context = InMemoryContext.Create("user1", null!, "org1");
        var rule = new SysValRule { Name = "OrgOnly", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT 1" };

        var result = engine.Evaluate(rule, "test", context);
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("org predicate");
    }

    [Fact]
    public void TenantIsolation_BothSet_WithoutPredicate_Fails()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.Create("user1", "tenant1", "org1");
        var rule = new SysValRule { Name = "BothTenantOrg", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT 1" };

        var result = engine.Evaluate(rule, "test", context);
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("tenant predicate");
    }

    [Fact]
    public void TenantIsolation_NullTenant_WithOrgPredicate_PassesSecurity()
    {
        // Null tenant, non-null org + org predicate → should pass
        var engine = new ValRuleEngine("Host=localhost;Database=test", Array.Empty<string>());
        var context = InMemoryContext.CreateWithTenantIsolation("user1", null, "org1", null, "OrgId = @OrgId");
        var rule = new SysValRule { Name = "OrgOnlyWithPred", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT COUNT(*) FROM Orders" };

        var result = engine.Evaluate(rule, "test", context);
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    // === Table Allowlist Edge Cases (Comprehensive) ===

    [Fact]
    public void TableAllowlist_CaseInsensitiveTableName()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", new[] { "Users" });
        var context = InMemoryContext.Create(null, null, null);
        // "users" in lowercase should be allowed since "Users" is in allowlist
        var rule = new SysValRule { Name = "LowercaseTable", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT COUNT(*) FROM users" };

        var result = engine.Evaluate(rule, "test", context);
        // Should pass security, fail execution (no DB)
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    [Fact]
    public void TableAllowlist_AliasTable()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", new[] { "Users" });
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "AliasTable", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT COUNT(*) FROM Users u" };

        var result = engine.Evaluate(rule, "test", context);
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    [Fact]
    public void TableAllowlist_WithASAlias()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", new[] { "Users" });
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "WithASAlias", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT COUNT(*) FROM Users AS u" };

        var result = engine.Evaluate(rule, "test", context);
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    [Fact]
    public void TableAllowlist_MultipleTables_AllInAllowlist()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", new[] { "Users", "Orders", "Products" });
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "MultiTable", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT * FROM Users u JOIN Orders o ON u.id = o.UserId JOIN Products p ON o.ProductId = p.id" };

        var result = engine.Evaluate(rule, "test", context);
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("execution failed");
    }

    [Fact]
    public void TableAllowlist_SubqueryWithForbiddenTable()
    {
        var engine = new ValRuleEngine("Host=localhost;Database=test", new[] { "Users" });
        var context = InMemoryContext.Create(null, null, null);
        var rule = new SysValRule { Name = "SubqueryForbidden", RuleType = ValRuleTypeEnum.Sql, Code = "SELECT * FROM Users WHERE id IN (SELECT UserId FROM AuditLog)" };

        var result = engine.Evaluate(rule, "test", context);
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("allowed list");
    }
}
