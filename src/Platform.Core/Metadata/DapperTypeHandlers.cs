using Dapper;
using Npgsql;

namespace Platform.Core.Metadata;

/// <summary>
/// Dapper type handlers for enum-to-string mapping in PostgreSQL.
/// Stores enums as VARCHAR strings; Dapper uses these handlers to round-trip values.
/// </summary>
public sealed class ValidationTypeEnumHandler : SqlMapper.TypeHandler<ValidationTypeEnum>
{
    public override void SetValue(System.Data.IDbDataParameter parameter, ValidationTypeEnum value)
    {
        parameter.Value = value.ToString();
    }

    public override ValidationTypeEnum Parse(object value)
    {
        return Enum.Parse<ValidationTypeEnum>(value.ToString()!, ignoreCase: true);
    }
}

public sealed class ValRuleTypeEnumHandler : SqlMapper.TypeHandler<ValRuleTypeEnum>
{
    public override void SetValue(System.Data.IDbDataParameter parameter, ValRuleTypeEnum value)
    {
        parameter.Value = value.ToString();
    }

    public override ValRuleTypeEnum Parse(object value)
    {
        return Enum.Parse<ValRuleTypeEnum>(value.ToString()!, ignoreCase: true);
    }
}

/// <summary>
/// Call this once during application startup to register enum type handlers with Dapper.
/// </summary>
public static class DapperTypeHandlers
{
    public static void Register()
    {
        SqlMapper.AddTypeHandler(new ValidationTypeEnumHandler());
        SqlMapper.AddTypeHandler(new ValRuleTypeEnumHandler());
    }
}
