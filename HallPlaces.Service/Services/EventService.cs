using AutoMapper;
using HallPlaces.Core.Common;
using HallPlaces.Core.DTOs;
using HallPlaces.Core.Entities;
using HallPlaces.Core.Interfaces.Repositories;
using HallPlaces.Core.Interfaces.Services;
using HallPlaces.Service.Helpers;

namespace HallPlaces.Service.Services;

public class EventService : IEventService
{
    private const int MaxPageSize = 50;

    private readonly IEventRepository _events;
    private readonly IHallRepository _halls;
    private readonly ISeatRepository _seats;
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public EventService(
        IEventRepository events,
        IHallRepository halls,
        ISeatRepository seats,
        ICategoryRepository categories,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _events = events;
        _halls = halls;
        _seats = seats;
        _categories = categories;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<Result<PagedResult<EventDto>>> GetPagedAsync(EventQuery query, CancellationToken ct)
    {
        // Never trust paging values that came from the URL.
        query.Page = Math.Max(query.Page, 1);
        query.PageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var page = await _events.GetPagedAsync(query, ct);

        // Same page, but with DTOs instead of entities.
        var result = new PagedResult<EventDto>
        {
            Items = _mapper.Map<List<EventDto>>(page.Items),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount
        };

        return Result<PagedResult<EventDto>>.Success(result);
    }

    public async Task<Result<EventDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var ev = await _events.GetDetailsAsync(id, ct);
        return ev is null
            ? Result<EventDto>.Fail(ErrorType.NotFound, "Event not found.")
            : Result<EventDto>.Success(_mapper.Map<EventDto>(ev));
    }

    public async Task<Result<IReadOnlyList<SeatDto>>> GetSeatsAsync(int eventId, CancellationToken ct)
    {
        var seats = await _seats.GetByEventAsync(eventId, ct);

        // Every event has at least one seat (a hall has at least one), so "no seats" means "no such event".
        if (seats.Count == 0)
        {
            return Result<IReadOnlyList<SeatDto>>.Fail(ErrorType.NotFound, "Event not found.");
        }

        return Result<IReadOnlyList<SeatDto>>.Success(_mapper.Map<List<SeatDto>>(seats));
    }

    public async Task<Result<EventDto>> CreateAsync(EventRequest request, CancellationToken ct)
    {
        var startsAt = request.StartsAt.ToUtc();
        var startError = ValidateStartsAt(startsAt);
        if (startError is not null)
        {
            return Result<EventDto>.Fail(ErrorType.Validation, startError);
        }

        var hall = await _halls.GetByIdAsync(request.HallId, ct);
        if (hall is null)
        {
            return Result<EventDto>.Fail(ErrorType.NotFound, "Hall not found.");
        }

        var categories = await ResolveCategoriesAsync(request.CategoryIds, ct);
        if (!categories.IsSuccess)
        {
            return Result<EventDto>.Fail(categories.ErrorType, categories.Error!);
        }

        var ev = _mapper.Map<Event>(request);
        ev.StartsAt = startsAt;
        ev.Hall = hall;
        ev.Categories = categories.Value!;
        ev.Seats = BuildSeats(hall.RowSeatCounts, request.SeatPrice);

        // One SaveChanges = one transaction: the event, its categories links and all its seats.
        await _events.AddAsync(ev, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<EventDto>.Success(_mapper.Map<EventDto>(ev));
    }

    public async Task<Result<EventDto>> UpdateAsync(int id, EventRequest request, CancellationToken ct)
    {
        var ev = await _events.GetForUpdateAsync(id, ct);
        if (ev is null)
        {
            return Result<EventDto>.Fail(ErrorType.NotFound, "Event not found.");
        }

        // The seats were copied from the hall, so moving the event to another hall is not allowed.
        // (Seat prices are also fixed when the event is created.)
        if (request.HallId != ev.HallId)
        {
            return Result<EventDto>.Fail(ErrorType.Validation, "The hall of an existing event cannot be changed.");
        }

        var startsAt = request.StartsAt.ToUtc();
        var startError = ValidateStartsAt(startsAt);
        if (startError is not null)
        {
            return Result<EventDto>.Fail(ErrorType.Validation, startError);
        }

        var categories = await ResolveCategoriesAsync(request.CategoryIds, ct);
        if (!categories.IsSuccess)
        {
            return Result<EventDto>.Fail(categories.ErrorType, categories.Error!);
        }

        _mapper.Map(request, ev);
        ev.StartsAt = startsAt;
        ev.Categories.Clear();
        foreach (var category in categories.Value!)
        {
            ev.Categories.Add(category);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return Result<EventDto>.Success(_mapper.Map<EventDto>(ev));
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct)
    {
        var ev = await _events.GetForUpdateAsync(id, ct);
        if (ev is null)
        {
            return Result.Fail(ErrorType.NotFound, "Event not found.");
        }

        // The booking history must not disappear.
        if (await _events.HasBookingsAsync(id, ct))
        {
            return Result.Fail(ErrorType.Conflict, "The event has bookings and cannot be deleted.");
        }

        _events.Remove(ev);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    // Shared by Create and Update (no duplicated code).
    private static string? ValidateStartsAt(DateTime startsAtUtc)
    {
        return startsAtUtc <= DateTime.UtcNow ? "The event must start in the future." : null;
    }

    // Loads all the requested categories with ONE query and checks that every id exists.
    private async Task<Result<List<Category>>> ResolveCategoriesAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return Result<List<Category>>.Success(new List<Category>());
        }

        var found = await _categories.GetByIdsAsync(distinctIds, ct);
        if (found.Count != distinctIds.Count)
        {
            return Result<List<Category>>.Fail(ErrorType.Validation, "One or more categories do not exist.");
        }

        return Result<List<Category>>.Success(found.ToList());
    }

    // The hall is only a template: each event gets its OWN copy of the seats,
    // so every seat can be locked and booked independently.
    private static List<Seat> BuildSeats(IReadOnlyList<int> rowSeatCounts, decimal basePrice)
    {
        var seats = new List<Seat>();
        for (var row = 0; row < rowSeatCounts.Count; row++)
        {
            var price = PriceForRow(row, rowSeatCounts.Count, basePrice);
            for (var number = 1; number <= rowSeatCounts[row]; number++)
            {
                seats.Add(new Seat { RowNumber = row + 1, SeatNumber = number, Price = price });
            }
        }

        return seats;
    }

    // Business rule: the closer to the stage, the higher the price.
    // The rows are split into three equal sections:
    //   front third  = base price x 1.5
    //   middle third = base price x 1.25
    //   back third   = base price (the SeatPrice the admin entered)
    // Rounded to a whole amount, and never below the base price.
    private static readonly decimal[] SectionMultipliers = { 1.5m, 1.25m, 1m };

    private static decimal PriceForRow(int rowIndex, int rowCount, decimal basePrice)
    {
        var section = rowIndex * SectionMultipliers.Length / rowCount;   // 0 = front, 1 = middle, 2 = back
        var price = Math.Round(basePrice * SectionMultipliers[section], 0, MidpointRounding.AwayFromZero);
        return Math.Max(price, basePrice);
    }
}
