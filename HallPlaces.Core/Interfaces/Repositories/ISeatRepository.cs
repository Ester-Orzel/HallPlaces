using HallPlaces.Core.Entities;

namespace HallPlaces.Core.Interfaces.Repositories;

public interface ISeatRepository
{
    // Read-only: the seat map of one event.
    Task<IReadOnlyList<Seat>> GetByEventAsync(int eventId, CancellationToken ct);

    // Tracked, with its Event: used when booking (we are going to change it).
    Task<Seat?> GetForBookingAsync(int seatId, CancellationToken ct);
}
