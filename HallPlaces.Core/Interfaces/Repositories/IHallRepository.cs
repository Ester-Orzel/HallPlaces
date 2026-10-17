using HallPlaces.Core.Entities;

namespace HallPlaces.Core.Interfaces.Repositories;

public interface IHallRepository : IRepository<Hall>
{
    // Read-only (AsNoTracking).
    Task<IReadOnlyList<Hall>> GetAllAsync(CancellationToken ct);

    // Tracked: used for update, delete, and to read the layout when creating an event.
    Task<Hall?> GetByIdAsync(int id, CancellationToken ct);

    Task<bool> HasEventsAsync(int hallId, CancellationToken ct);
}
