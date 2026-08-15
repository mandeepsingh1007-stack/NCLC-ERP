using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// Base interface for all persistent objects managed by the platform.
/// Generated X_<Table> classes implement this. M_<Table> classes extend it.
/// </summary>
public interface IPersistentObject
{
    int SysTableId { get; set; }
    void Load(int id, IReadOnlyContext context);
    int Save(IReadOnlyContext context);
    void Delete(IReadOnlyContext context);
}
