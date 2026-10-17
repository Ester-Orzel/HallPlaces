using HallPlaces.Core.Common;
using HallPlaces.Core.Entities;

namespace HallPlaces.Core.Interfaces.Repositories;

public interface IEventRepository : IRepository<Event>
{
    // Read-only list with filter, sort and real DB paging (Skip/Take in SQL).
    Task<PagedResult<Event>> GetPagedAsync(EventQuery query, CancellationToken ct);

    // Read-only, with Hall + Categories (for display).
    Task<Event?> GetDetailsAsync(int id, CancellationToken ct);

    // Tracked, with Categories (for update).
    Task<Event?> GetForUpdateAsync(int id, CancellationToken ct);

    Task<bool> HasBookingsAsync(int eventId, CancellationToken ct);
}
