using HallPlaces.Core.Entities;
using HallPlaces.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HallPlaces.Data.Repositories;

public class SeatRepository : ISeatRepository
{
    private readonly AppDbContext _context;

    public SeatRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Seat>> GetByEventAsync(int eventId, CancellationToken ct)
    {
        return await _context.Seats
            .AsNoTracking()
            .Where(s => s.EventId == eventId)
            .OrderBy(s => s.RowNumber)
            .ThenBy(s => s.SeatNumber)
            .ToListAsync(ct);
    }

    // Tracked on purpose (no AsNoTracking): the Service is going to change this seat,
    // and EF Core must remember the Version we read so it can detect a conflict on save.
    public async Task<Seat?> GetForBookingAsync(int seatId, CancellationToken ct)
    {
        return await _context.Seats
            .Include(s => s.Event)
            .FirstOrDefaultAsync(s => s.Id == seatId, ct);
    }
}
