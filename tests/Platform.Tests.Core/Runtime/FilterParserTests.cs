using Platform.Core.Runtime;

namespace Platform.Tests.Core.Runtime;

public class FilterParserTests
{
    private static readonly string[] SampleColumns = { "Status", "Name", "Amount", "CreatedDate", "Type", "DeletedDate", "Priority" };

    [Fact]
    public void Parse_NullFilter_ReturnsEmptyWhere()
    {
        var parser = new FilterParser();
        var result = parser.Parse(null, SampleColumns);
        Assert.Empty(result.SqlWhereClause);
        Assert.Empty(result.Parameters);
        Assert.Equal(0, result.ClauseCount);
    }

    [Fact]
    public void Parse_EmptyFilter_ReturnsEmptyWhere()
    {
        var parser = new FilterParser();
        var result = parser.Parse("", SampleColumns);
        Assert.Empty(result.SqlWhereClause);
    }

    [Fact]
    public void Parse_SimpleEq_CorrectSqlAndParameter()
    {
        var parser = new FilterParser();
        var filter = @"{ ""column"": ""Status"", ""op"": ""eq"", ""value"": ""Active"" }";
        var result = parser.Parse(filter, SampleColumns);

        Assert.Contains("WHERE", result.SqlWhereClause);
        Assert.Contains("Status", result.SqlWhereClause);
        Assert.Single(result.Parameters);
        Assert.Equal("Active", result.Parameters[0].Value);
        Assert.Equal(1, result.ClauseCount);
    }

    [Fact]
    public void Parse_NumericGt_CorrectSqlAndParameter()
    {
        var parser = new FilterParser();
        var filter = @"{ ""column"": ""Amount"", ""op"": ""gt"", ""value"": 1000 }";
        var result = parser.Parse(filter, SampleColumns);

        Assert.Contains("Amount", result.SqlWhereClause);
        Assert.Contains(">", result.SqlWhereClause);
        Assert.Equal(1000.0, result.Parameters[0].Value); // double from JSON
        Assert.Equal(1, result.ClauseCount);
    }

    [Fact]
    public void Parse_Like_CorrectSqlAndParameter()
    {
        var parser = new FilterParser();
        var filter = @"{ ""column"": ""Name"", ""op"": ""like"", ""value"": ""%test%"" }";
        var result = parser.Parse(filter, SampleColumns);

        Assert.Contains("LIKE", result.SqlWhereClause);
        Assert.Equal(1, result.ClauseCount);
    }

    [Fact]
    public void Parse_Ilike_CorrectSqlAndParameter()
    {
        var parser = new FilterParser();
        var filter = @"{ ""column"": ""Name"", ""op"": ""ilike"", ""value"": ""test%"" }";
        var result = parser.Parse(filter, SampleColumns);

        Assert.Contains("ILIKE", result.SqlWhereClause);
        Assert.Equal(1, result.ClauseCount);
    }

    [Fact]
    public void Parse_NullColumn_ThrowsArgumentException()
    {
        var parser = new FilterParser();
        var filter = @"{ ""op"": ""eq"", ""value"": ""Active"" }";
        Assert.Throws<ArgumentException>(() => parser.Parse(filter, SampleColumns));
    }

    [Fact]
    public void Parse_UnknownColumn_ThrowsArgumentException()
    {
        var parser = new FilterParser();
        var filter = @"{ ""column"": ""NonExistent"", ""op"": ""eq"", ""value"": ""Active"" }";
        Assert.Throws<ArgumentException>(() => parser.Parse(filter, SampleColumns));
    }

    [Fact]
    public void Parse_UnknownOperator_ThrowsArgumentException()
    {
        var parser = new FilterParser();
        var filter = @"{ ""column"": ""Status"", ""op"": ""regex"", ""value"": ""test"" }";
        Assert.Throws<ArgumentException>(() => parser.Parse(filter, SampleColumns));
    }

    [Fact]
    public void Parse_BooleanAnd_CorrectlyJoinsWithAnd()
    {
        var parser = new FilterParser();
        var filter = @"{ ""type"": ""boolean"", ""op"": ""$and"", ""clauses"": [
            { ""column"": ""Status"", ""op"": ""eq"", ""value"": ""Active"" },
            { ""column"": ""Type"", ""op"": ""eq"", ""value"": ""Standard"" }
        ]}";
        var result = parser.Parse(filter, SampleColumns);

        Assert.Contains("AND", result.SqlWhereClause);
        Assert.Contains("Status", result.SqlWhereClause);
        Assert.Contains("Type", result.SqlWhereClause);
        Assert.Equal(2, result.ClauseCount);
    }

    [Fact]
    public void Parse_BooleanOr_CorrectlyJoinsWithOr()
    {
        var parser = new FilterParser();
        var filter = @"{ ""type"": ""boolean"", ""op"": ""$or"", ""clauses"": [
            { ""column"": ""Status"", ""op"": ""eq"", ""value"": ""Active"" },
            { ""column"": ""Status"", ""op"": ""eq"", ""value"": ""Pending"" }
        ]}";
        var result = parser.Parse(filter, SampleColumns);

        Assert.Contains("OR", result.SqlWhereClause);
        Assert.Equal(2, result.ClauseCount);
    }

    [Fact]
    public void Parse_NestedAndOr_CorrectlyCombines()
    {
        var parser = new FilterParser();
        var filter = @"{ ""type"": ""boolean"", ""op"": ""$and"", ""clauses"": [
            { ""column"": ""Status"", ""op"": ""eq"", ""value"": ""Active"" },
            { ""type"": ""boolean"", ""op"": ""$or"", ""clauses"": [
                { ""column"": ""Amount"", ""op"": ""gt"", ""value"": 1000 },
                { ""column"": ""Priority"", ""op"": ""eq"", ""value"": ""High"" }
            ]}
        ]}";
        var result = parser.Parse(filter, SampleColumns);

        Assert.Contains("AND", result.SqlWhereClause);
        Assert.Contains("OR", result.SqlWhereClause);
        Assert.Equal(3, result.ClauseCount);
    }

    [Fact]
    public void Parse_InOperator_CorrectSql()
    {
        var parser = new FilterParser();
        var filter = @"{ ""column"": ""Status"", ""op"": ""in"", ""values"": [""Active"", ""Pending""] }";
        var result = parser.Parse(filter, SampleColumns);

        Assert.Contains("IN", result.SqlWhereClause);
        Assert.Equal(2, result.ClauseCount);
    }

    [Fact]
    public void Parse_NotInOperator_CorrectSql()
    {
        var parser = new FilterParser();
        var filter = @"{ ""column"": ""Status"", ""op"": ""not in"", ""values"": [""Deleted""] }";
        var result = parser.Parse(filter, SampleColumns);

        Assert.Contains("NOT IN", result.SqlWhereClause);
        Assert.Equal(1, result.ClauseCount);
    }

    [Fact]
    public void Parse_BetweenOperator_CorrectSql()
    {
        var parser = new FilterParser();
        var filter = @"{ ""column"": ""Amount"", ""op"": ""between"", ""values"": [100, 1000] }";
        var result = parser.Parse(filter, SampleColumns);

        Assert.Contains("BETWEEN", result.SqlWhereClause);
        Assert.Equal(1, result.ClauseCount);
    }

    [Fact]
    public void Parse_NullOperator_CorrectSql()
    {
        var parser = new FilterParser();
        var filter = @"{ ""column"": ""DeletedDate"", ""op"": ""null"" }";
        var result = parser.Parse(filter, SampleColumns);

        Assert.Contains("IS NULL", result.SqlWhereClause);
        Assert.Equal(1, result.ClauseCount);
        Assert.Empty(result.Parameters);
    }

    [Fact]
    public void Parse_NotNullOperator_CorrectSql()
    {
        var parser = new FilterParser();
        var filter = @"{ ""column"": ""DeletedDate"", ""op"": ""notnull"" }";
        var result = parser.Parse(filter, SampleColumns);

        Assert.Contains("IS NOT NULL", result.SqlWhereClause);
        Assert.Equal(1, result.ClauseCount);
        Assert.Empty(result.Parameters);
    }

    [Fact]
    public void Parse_NestingDepthExceeds10_ThrowsArgumentException()
    {
        var parser = new FilterParser();
        // Build a deeply nested filter: 11 levels of $and
        var deep = """{"type":"boolean","op":"$and","clauses":[{"type":"boolean","op":"$and","clauses":[{"type":"boolean","op":"$and","clauses":[{"type":"boolean","op":"$and","clauses":[{"type":"boolean","op":"$and","clauses":[{"type":"boolean","op":"$and","clauses":[{"type":"boolean","op":"$and","clauses":[{"type":"boolean","op":"$and","clauses":[{"type":"boolean","op":"$and","clauses":[{"type":"boolean","op":"$and","clauses":[{"column":"Status","op":"eq","value":"Active"}]}]}]}]}]}]}]}]}]}]}""";

        // This should throw due to depth > 10
        Assert.Throws<ArgumentException>(() => parser.Parse(deep, SampleColumns));
    }

    [Fact]
    public void Parse_Exceeds4096Characters_ThrowsArgumentException()
    {
        var parser = new FilterParser();
        // Build a filter > 4096 chars
        var filter = @"{ ""column"": ""Status"", ""op"": ""eq"", ""value"": """ + new string('a', 4100) + @""" }";
        Assert.Throws<ArgumentException>(() => parser.Parse(filter, SampleColumns));
    }

    [Fact]
    public void Parse_CompactNotation_CorrectlyAnds()
    {
        var parser = new FilterParser();
        var filter = @"{ ""Status"": { ""op"": ""eq"", ""value"": ""Active"" }, ""Type"": { ""op"": ""eq"", ""value"": ""Standard"" } }";
        var result = parser.Parse(filter, SampleColumns);

        Assert.Contains("AND", result.SqlWhereClause);
        Assert.Contains("Status", result.SqlWhereClause);
        Assert.Contains("Type", result.SqlWhereClause);
        Assert.Equal(2, result.ClauseCount);
    }

    [Fact]
    public void Parse_NotOperator_ProducesNotClause()
    {
        var parser = new FilterParser();
        var filter = @"{ ""type"": ""boolean"", ""op"": ""$not"", ""clauses"": [
            { ""column"": ""Status"", ""op"": ""eq"", ""value"": ""Deleted"" }
        ]}";
        var result = parser.Parse(filter, SampleColumns);

        Assert.Contains("NOT", result.SqlWhereClause);
        Assert.Equal(1, result.ClauseCount);
    }

    [Fact]
    public void Parse_CaseInsensitiveColumnMatch()
    {
        var parser = new FilterParser();
        var filter = @"{ ""column"": ""status"", ""op"": ""eq"", ""value"": ""Active"" }";
        var result = parser.Parse(filter, SampleColumns);

        // Should succeed because column check is case-insensitive
        Assert.Contains("Status", result.SqlWhereClause);
    }

    [Fact]
    public void Parse_MultipleParameters_BeDifferentParameterNames()
    {
        var parser = new FilterParser();
        var filter = @"{ ""type"": ""boolean"", ""op"": ""$and"", ""clauses"": [
            { ""column"": ""Status"", ""op"": ""eq"", ""value"": ""Active"" },
            { ""column"": ""Type"", ""op"": ""eq"", ""value"": ""Standard"" }
        ]}";
        var result = parser.Parse(filter, SampleColumns);

        Assert.Equal(2, result.Parameters.Length);
    }

    [Fact]
    public void Parse_LtOperator_CorrectSql()
    {
        var parser = new FilterParser();
        var filter = @"{ ""column"": ""Amount"", ""op"": ""lt"", ""value"": 500 }";
        var result = parser.Parse(filter, SampleColumns);

        Assert.Contains("<", result.SqlWhereClause);
        Assert.Equal(1, result.ClauseCount);
    }

    [Fact]
    public void Parse_GteOperator_CorrectSql()
    {
        var parser = new FilterParser();
        var filter = @"{ ""column"": ""Amount"", ""op"": ""gte"", ""value"": 100 }";
        var result = parser.Parse(filter, SampleColumns);

        Assert.Contains(">=", result.SqlWhereClause);
        Assert.Equal(1, result.ClauseCount);
    }

    [Fact]
    public void Parse_EmptyInValues_ReturnsAlwaysFalse()
    {
        var parser = new FilterParser();
        var filter = @"{ ""column"": ""Status"", ""op"": ""in"", ""values"": [] }";
        var result = parser.Parse(filter, SampleColumns);

        // Empty IN should return 1=0 (always false)
        Assert.Contains("1=0", result.SqlWhereClause);
    }
}
