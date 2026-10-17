using HallPlaces.Core.Entities;

namespace HallPlaces.Core.Interfaces.Repositories;

public interface IBookingRepository : IRepository<Booking>
{
    // Read-only: Booking -> Seat -> Event (Include + ThenInclude). "My bookings".
    Task<IReadOnlyList<Booking>> GetByUserAsync(int userId, CancellationToken ct);

    // Read-only: Booking -> User, Booking -> Seat. Admin view of one event.
    Task<IReadOnlyList<Booking>> GetByEventAsync(int eventId, CancellationToken ct);

    // Tracked, with Seat and Seat.Event: used when cancelling.
    Task<Booking?> GetForCancelAsync(int bookingId, CancellationToken ct);
}
