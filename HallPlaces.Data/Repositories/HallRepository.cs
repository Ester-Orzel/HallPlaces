using HallPlaces.Core.Entities;
using HallPlaces.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HallPlaces.Data.Repositories;

public class HallRepository : Repository<Hall>, IHallRepository
{
    public HallRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Hall>> GetAllAsync(CancellationToken ct)
    {
        return await Context.Halls
            .AsNoTracking()
            .OrderBy(h => h.Name)
            .ToListAsync(ct);
    }

    public async Task<Hall?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await Context.Halls.FirstOrDefaultAsync(h => h.Id == id, ct);
    }

    public async Task<bool> HasEventsAsync(int hallId, CancellationToken ct)
    {
        return await Context.Events.AnyAsync(e => e.HallId == hallId, ct);
    }
}
