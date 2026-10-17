namespace HallPlaces.Core.Interfaces.Repositories;

// Saves all changes made through the repositories in ONE database transaction.
public interface IUnitOfWork
{
    /// <exception cref="Exceptions.ConcurrencyConflictException">
    /// When another user changed the same row between our read and our save.
    /// </exception>
    Task<int> SaveChangesAsync(CancellationToken ct);
}
