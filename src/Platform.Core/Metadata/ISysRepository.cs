namespace Platform.Core.Metadata;

/// <summary>
/// Generic repository interface for dictionary entities with single-column primary keys.
/// </summary>
public interface ISysRepository<T> where T : ISysEntity
{
    T? GetById(int id);
    IEnumerable<T> GetAll();
    int Create(T entity);
    void Update(T entity);
    void Delete(int id);
}

/// <summary>
/// Repository interface for entities with composite or non-int primary keys.
/// </summary>
public interface ISysCompositeRepository<T> where T : ISysEntity
{
    IEnumerable<T> GetAll();
    int Create(T entity);
    void Update(T entity);
    void Delete(params object[] keyValues);
}
