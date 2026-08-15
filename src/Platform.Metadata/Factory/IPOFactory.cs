namespace Platform.Metadata.Factory;

/// <summary>
/// Resolves PO factory classes (M_ and X_) for dictionary tables.
/// Assembly whitelist: only Platform.Metadata assembly.
/// No arbitrary Assembly.Load with user input.
/// Type names validated against regex ^M_\\w+$ or ^X_\\w+$.
/// </summary>
public interface IPOFactory
{
    Type? ResolveMClass(string tableName);
    Type? ResolveXClass(string tableName);
    object? CreateInstance(string tableName);
    IReadOnlyList<string> GetRegisteredTables();
}
