using HallPlaces.Core.Common;
using HallPlaces.Core.DTOs;

namespace HallPlaces.Core.Interfaces.Services;

public interface IEventService
{
    Task<Result<PagedResult<EventDto>>> GetPagedAsync(EventQuery query, CancellationToken ct);
    Task<Result<EventDto>> GetByIdAsync(int id, CancellationToken ct);
    Task<Result<IReadOnlyList<SeatDto>>> GetSeatsAsync(int eventId, CancellationToken ct);
    Task<Result<EventDto>> CreateAsync(EventRequest request, CancellationToken ct);
    Task<Result<EventDto>> UpdateAsync(int id, EventRequest request, CancellationToken ct);
    Task<Result> DeleteAsync(int id, CancellationToken ct);
}
