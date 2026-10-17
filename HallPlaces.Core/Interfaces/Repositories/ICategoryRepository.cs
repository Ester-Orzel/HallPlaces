using HallPlaces.Core.Entities;

namespace HallPlaces.Core.Interfaces.Repositories;

public interface ICategoryRepository : IRepository<Category>
{
    // Read-only (AsNoTracking).
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct);

    // Tracked: used for update and delete.
    Task<Category?> GetByIdAsync(int id, CancellationToken ct);

    // Tracked: used to attach categories to an event.
    Task<IReadOnlyList<Category>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken ct);

    Task<bool> NameExistsAsync(string name, CancellationToken ct);
}
