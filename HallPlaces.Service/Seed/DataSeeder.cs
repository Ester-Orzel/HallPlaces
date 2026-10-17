using HallPlaces.Core.Common;
using HallPlaces.Core.DTOs;
using HallPlaces.Core.Entities;
using HallPlaces.Core.Enums;
using HallPlaces.Core.Interfaces.Repositories;
using HallPlaces.Core.Interfaces.Security;
using HallPlaces.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace HallPlaces.Service.Seed;

// Fills an EMPTY database with demo data, so the system works right after the first run.
// Safe to run on every startup: it adds only what is missing.
//
// It uses the SAME Services as the API (no duplicated logic): for example the seats of a seeded
// event are created by EventService, exactly as when an admin creates an event.
//
// The passwords below are DEMO passwords, written in the README. Real secrets never live here.
public class DataSeeder
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICategoryService _categoryService;
    private readonly IHallService _hallService;
    private readonly IEventService _eventService;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        ICategoryService categoryService,
        IHallService hallService,
        IEventService eventService,
        ILogger<DataSeeder> logger)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _categoryService = categoryService;
        _hallService = hallService;
        _eventService = eventService;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct)
    {
        await SeedUsersAsync(ct);
        await SeedCatalogAsync(ct);
    }

    private async Task SeedUsersAsync(CancellationToken ct)
    {
        await AddUserIfMissingAsync("Admin User", "admin@hallplaces.com", "Admin123!", UserRole.Admin, ct);
        await AddUserIfMissingAsync("Dana Customer", "dana@hallplaces.com", "Customer123!", UserRole.Customer, ct);
        await AddUserIfMissingAsync("Ron Customer", "ron@hallplaces.com", "Customer123!", UserRole.Customer, ct);

        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task AddUserIfMissingAsync(string fullName, string email, string password, UserRole role, CancellationToken ct)
    {
        if (await _users.EmailExistsAsync(email, ct))
        {
            return;
        }

        await _users.AddAsync(new User
        {
            FullName = fullName,
            Email = email,
            PasswordHash = _passwordHasher.Hash(password),
            Role = role
        }, ct);

        _logger.LogInformation("Seeding the demo user {Email} ({Role})", email, role);
    }

    private async Task SeedCatalogAsync(CancellationToken ct)
    {
        // Already has halls = the catalog was seeded before (or an admin created his own data).
        if (Unwrap(await _hallService.GetAllAsync(ct)).Count > 0)
        {
            return;
        }

        _logger.LogInformation("Seeding the demo halls, categories and events");

        var music = await CreateCategoryAsync("Music", ct);
        var theater = await CreateCategoryAsync("Theater", ct);
        var comedy = await CreateCategoryAsync("Comedy", ct);
        await CreateCategoryAsync("Conference", ct);

        var mainHall = await CreateHallAsync("Main Hall", "1 Herzl Street, Tel Aviv", new List<int> { 8, 10, 10, 12 }, ct);
        var studio = await CreateHallAsync("Small Studio", "12 Rothschild Blvd, Tel Aviv", new List<int> { 6, 6, 8 }, ct);

        await CreateEventAsync("Rock Night", "An evening of live rock music.", 14, mainHall, 120m, new[] { music }, ct);
        await CreateEventAsync("Hamlet", "A classic play in a new production.", 21, mainHall, 90m, new[] { theater }, ct);
        await CreateEventAsync("Stand-up Evening", "Three comedians, one stage.", 10, studio, 60m, new[] { comedy }, ct);
    }

    private async Task<int> CreateCategoryAsync(string name, CancellationToken ct)
    {
        return Unwrap(await _categoryService.CreateAsync(new CategoryRequest { Name = name }, ct)).Id;
    }

    private async Task<int> CreateHallAsync(string name, string address, List<int> rowSeatCounts, CancellationToken ct)
    {
        var request = new HallRequest { Name = name, Address = address, RowSeatCounts = rowSeatCounts };
        return Unwrap(await _hallService.CreateAsync(request, ct)).Id;
    }

    private async Task CreateEventAsync(
        string title, string description, int daysFromNow, int hallId, decimal seatPrice, int[] categoryIds, CancellationToken ct)
    {
        var request = new EventRequest
        {
            Title = title,
            Description = description,
            StartsAt = DateTime.UtcNow.Date.AddDays(daysFromNow).AddHours(18),   // 18:00 UTC, always in the future
            HallId = hallId,
            SeatPrice = seatPrice,
            CategoryIds = categoryIds.ToList()
        };

        Unwrap(await _eventService.CreateAsync(request, ct));
    }

    // A failure while seeding is a bug in the seed itself, so we stop loudly instead of continuing half-done.
    private static T Unwrap<T>(Result<T> result)
    {
        return result.IsSuccess
            ? result.Value!
            : throw new InvalidOperationException($"Seeding failed: {result.Error}");
    }
}
