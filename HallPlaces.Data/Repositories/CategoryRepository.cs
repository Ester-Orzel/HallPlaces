using HallPlaces.Core.Entities;
using HallPlaces.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HallPlaces.Data.Repositories;

public class CategoryRepository : Repository<Category>, ICategoryRepository
{
    public CategoryRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct)
    {
        return await Context.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task<Category?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await Context.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    // ONE query for all the ids (WHERE "Id" = ANY(...)), not one query per id.
    public async Task<IReadOnlyList<Category>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var idList = ids.ToList();
        return await Context.Categories
            .Where(c => idList.Contains(c.Id))
            .ToListAsync(ct);
    }

    public async Task<bool> NameExistsAsync(string name, CancellationToken ct)
    {
        return await Context.Categories.AnyAsync(c => c.Name == name, ct);
    }
}
