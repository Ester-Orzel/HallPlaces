using HallPlaces.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HallPlaces.Data.Repositories;

// The operations that are identical for EVERY entity, written once.
// Every specific repository inherits them and adds only its own queries.
public abstract class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext Context;

    protected Repository(AppDbContext context)
    {
        Context = context;
    }

    // Only marks the entity as "to be inserted". Nothing reaches the database
    // until IUnitOfWork.SaveChangesAsync is called.
    public async Task AddAsync(T entity, CancellationToken ct)
    {
        await Context.Set<T>().AddAsync(entity, ct);
    }

    public void Remove(T entity)
    {
        Context.Set<T>().Remove(entity);
    }
}
