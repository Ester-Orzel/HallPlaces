using HallPlaces.Core.Exceptions;
using HallPlaces.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HallPlaces.Data.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    // Saves everything the repositories prepared, in ONE database transaction.
    // This is the only place that knows EF Core's / PostgreSQL's exceptions:
    // it translates them to ConcurrencyConflictException, which belongs to Core.
    public async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            return await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Someone changed the same row between our read and our save (the Version no longer matches).
            throw new ConcurrencyConflictException("The data was changed by another user.", ex);
        }
        catch (DbUpdateException ex) when (IsActiveBookingConflict(ex))
        {
            // Safety net: the database refused a second ACTIVE booking for the same seat.
            throw new ConcurrencyConflictException("The seat was booked by another user.", ex);
        }
    }

    private static bool IsActiveBookingConflict(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Bookings_SeatId"
        };
    }
}
