using System.Collections.Concurrent;
using System.Reflection;
using Platform.Core.Runtime;

namespace Platform.Metadata.Factory;

/// <summary>
/// Deterministic PO class factory.
///
/// Resolution order:
/// 1. Check cache key `factory:M:{tableName}`
/// 2. Search `Platform.Metadata` assembly for class `M_{TableName}`
/// 3. Fallback to `X_<Table>` from SysTable.ClassName (via MetadataGraph)
///
/// Security:
/// - Only load from Platform.Metadata assembly
/// - Never use Assembly.Load(string) with user input
/// - Type names validated against regex ^M_\w+$ or ^X_\w+$
/// - Pre-cached type dictionary at startup
/// </summary>
public class POFactory : IPOFactory, IDisposable
{
    private const string AllowedAssemblyName = "Platform.Metadata";
    private static readonly System.Text.RegularExpressions.Regex TypeNameRegex =
        new(@"^(M_|X_)\w+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private readonly ConcurrentDictionary<string, Type?> _mClassCache = new();
    private readonly ConcurrentDictionary<string, Type?> _xClassCache = new();
    private readonly IMetadataGraph _metadataGraph;
    private readonly Assembly _metadataAssembly;
    private readonly TimeSpan _cacheTtl = TimeSpan.FromHours(1);
    private bool _disposed;

    public POFactory(IMetadataGraph metadataGraph)
    {
        _metadataGraph = metadataGraph;
        _metadataAssembly = Assembly.Load(AllowedAssemblyName);
    }

    /// <summary>
    /// Resolve the M_<Table> business logic class for a given table.
    /// Returns null if the class doesn't exist or is not from the allowed assembly.
    /// </summary>
    public Type? ResolveMClass(string tableName)
    {
        if (!ValidateTableName(tableName))
        {
            return null;
        }

        var cacheKey = $"factory:M:{tableName}";
        return _mClassCache.GetOrAdd(cacheKey, _ =>
        {
            var className = $"M_{tableName}";
            var type = ResolveType(className, _metadataAssembly);
            return type;
        });
    }

    /// <summary>
    /// Resolve the X_<Table> generated class for a given table.
    /// Returns null if the class doesn't exist or is not from the allowed assembly.
    /// </summary>
    public Type? ResolveXClass(string tableName)
    {
        var cacheKey = $"factory:X:{tableName}";
        return _xClassCache.GetOrAdd(cacheKey, _ =>
        {
            // First try generated class name from SysTable.ClassName
            var tableInfo = _metadataGraph.GetTable(tableName);
            if (tableInfo != null && !string.IsNullOrEmpty(tableInfo.ClassName))
            {
                var className = tableInfo.ClassName;
                if (ValidateClassName(className))
                {
                    var type = ResolveType(className, _metadataAssembly);
                    if (type != null) return type;
                }
            }

            // Fallback: X_<TableName>
            return ResolveType($"X_{tableName}", _metadataAssembly);
        });
    }

    /// <summary>
    /// Create an instance of the PO class for a given table.
    /// Returns null if the class can't be resolved or instantiated.
    /// </summary>
    public object? CreateInstance(string tableName)
    {
        // Try M_ class first (business logic), fall back to X_ class (generated)
        var mType = ResolveMClass(tableName);
        if (mType != null)
        {
            return Activator.CreateInstance(mType);
        }

        var xType = ResolveXClass(tableName);
        if (xType != null)
        {
            return Activator.CreateInstance(xType);
        }

        return null;
    }

    /// <summary>
    /// Get the list of tables that have registered PO classes.
    /// </summary>
    public IReadOnlyList<string> GetRegisteredTables()
    {
        var tables = _metadataGraph.GetTableNames();
        return tables.Where(t => ResolveMClass(t) != null || ResolveXClass(t) != null).ToList().AsReadOnly();
    }

    /// <summary>
    /// Validate that a table name contains only safe characters.
    /// </summary>
    private static bool ValidateTableName(string tableName)
    {
        return !string.IsNullOrEmpty(tableName) &&
               System.Text.RegularExpressions.Regex.IsMatch(tableName, @"^[A-Za-z][A-Za-z0-9_]*$");
    }

    /// <summary>
    /// Validate that a class name follows the M_/X_ naming convention.
    /// </summary>
    private static bool ValidateClassName(string className)
    {
        return !string.IsNullOrEmpty(className) && TypeNameRegex.IsMatch(className);
    }

    /// <summary>
    /// Resolve a type from the allowed assembly.
    /// Never uses Assembly.Load(string) with user input.
    /// </summary>
    private Type? ResolveType(string typeName, Assembly allowedAssembly)
    {
        if (!ValidateClassName(typeName))
        {
            return null;
        }

        // Only resolve from the allowed assembly
        var type = allowedAssembly.GetType(typeName, throwOnError: false);
        return type;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
