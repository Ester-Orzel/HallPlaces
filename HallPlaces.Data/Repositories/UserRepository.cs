using HallPlaces.Core.Entities;
using HallPlaces.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HallPlaces.Data.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    // Used for login: we only read the user, so no tracking.
    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct)
    {
        return await Context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct)
    {
        return await Context.Users.AnyAsync(u => u.Email == email, ct);
    }
}
