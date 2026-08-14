namespace Platform.Core.Metadata;

/// <summary>
/// Generic repository interface for dictionary entities.
/// </summary>
public interface ISysRepository<T> where T : ISysEntity
{
    T? GetById(int id);
    IEnumerable<T> GetAll();
    int Create(T entity);
    void Update(T entity);
    void Delete(int id);
}
