using HallPlaces.Core.Entities;
using HallPlaces.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HallPlaces.Data.Repositories;

public class BookingRepository : Repository<Booking>, IBookingRepository
{
    public BookingRepository(AppDbContext context) : base(context) { }

    // "My bookings": Booking -> Seat -> Event, all in ONE query.
    public async Task<IReadOnlyList<Booking>> GetByUserAsync(int userId, CancellationToken ct)
    {
        return await Context.Bookings
            .AsNoTracking()
            .Include(b => b.Seat)
                .ThenInclude(s => s.Event)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);
    }

    // Admin view: who booked which seat in an event.
    public async Task<IReadOnlyList<Booking>> GetByEventAsync(int eventId, CancellationToken ct)
    {
        return await Context.Bookings
            .AsNoTracking()
            .Include(b => b.User)
            .Include(b => b.Seat)
                .ThenInclude(s => s.Event)
            .Where(b => b.Seat.EventId == eventId)
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(ct);
    }

    // Tracked: the Service cancels the booking and frees the seat.
    public async Task<Booking?> GetForCancelAsync(int bookingId, CancellationToken ct)
    {
        return await Context.Bookings
            .Include(b => b.Seat)
                .ThenInclude(s => s.Event)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);
    }
}
