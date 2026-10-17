using HallPlaces.Core.Common;
using HallPlaces.Core.Entities;
using HallPlaces.Core.Enums;
using HallPlaces.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HallPlaces.Data.Repositories;

public class EventRepository : Repository<Event>, IEventRepository
{
    public EventRepository(AppDbContext context) : base(context) { }

    // The Service guarantees Page >= 1 and a sane PageSize before calling this.
    public async Task<PagedResult<Event>> GetPagedAsync(EventQuery query, CancellationToken ct)
    {
        // Nothing runs yet: this only BUILDS a query, step by step.
        IQueryable<Event> events = Context.Events.AsNoTracking();

        if (query.UpcomingOnly)
        {
            events = events.Where(e => e.StartsAt > DateTime.UtcNow);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            events = events.Where(e => e.Title.ToLower().Contains(search));
        }

        if (query.CategoryId.HasValue)
        {
            var categoryId = query.CategoryId.Value;
            events = events.Where(e => e.Categories.Any(c => c.Id == categoryId));
        }

        // Query 1: how many events match in total (for "page 2 of 7").
        var totalCount = await events.CountAsync(ct);

        // Query 2: only the requested page. Skip/Take run INSIDE the database (LIMIT / OFFSET).
        // Hall and Categories are loaded in the same query (Include), so no N+1.
        var items = await ApplySort(events.Include(e => e.Hall).Include(e => e.Categories), query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Event>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<Event?> GetDetailsAsync(int id, CancellationToken ct)
    {
        return await Context.Events
            .AsNoTracking()
            .Include(e => e.Hall)
            .Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<Event?> GetForUpdateAsync(int id, CancellationToken ct)
    {
        return await Context.Events
            .Include(e => e.Hall)
            .Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    // ANY booking (active or cancelled) blocks deleting the event,
    // because the booking history must not be lost.
    public async Task<bool> HasBookingsAsync(int eventId, CancellationToken ct)
    {
        return await Context.Bookings.AnyAsync(b => b.Seat.EventId == eventId, ct);
    }

    private static IOrderedQueryable<Event> ApplySort(IQueryable<Event> events, EventQuery query)
    {
        var sorted = query.SortBy == EventSortBy.Title
            ? (query.Descending ? events.OrderByDescending(e => e.Title) : events.OrderBy(e => e.Title))
            : (query.Descending ? events.OrderByDescending(e => e.StartsAt) : events.OrderBy(e => e.StartsAt));

        // Tie-breaker: two events with the same title/date must always come in the same order,
        // otherwise an event can appear on two pages, or on none.
        return sorted.ThenBy(e => e.Id);
    }
}
