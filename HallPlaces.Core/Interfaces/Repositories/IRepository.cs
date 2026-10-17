namespace HallPlaces.Core.Interfaces.Repositories;

// Operations that are identical for every entity, written ONCE.
// Each specific repository inherits these and adds only its own queries.
public interface IRepository<T> where T : class
{
    Task AddAsync(T entity, CancellationToken ct);
    void Remove(T entity);
}
