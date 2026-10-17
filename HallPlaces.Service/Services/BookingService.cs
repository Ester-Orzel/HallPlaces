using AutoMapper;
using HallPlaces.Core.Common;
using HallPlaces.Core.DTOs;
using HallPlaces.Core.Entities;
using HallPlaces.Core.Enums;
using HallPlaces.Core.Exceptions;
using HallPlaces.Core.Interfaces.Repositories;
using HallPlaces.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace HallPlaces.Service.Services;

// The heart of the project: the limited resource is a SEAT.
// Two users may try to book the same seat at the same moment - exactly one must win.
public class BookingService : IBookingService
{
    private readonly ISeatRepository _seats;
    private readonly IBookingRepository _bookings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        ISeatRepository seats,
        IBookingRepository bookings,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<BookingService> logger)
    {
        _seats = seats;
        _bookings = bookings;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<BookingDto>> BookAsync(int userId, BookingRequest request, CancellationToken ct)
    {
        // The seat is loaded TRACKED: EF Core remembers its Version as we read it.
        var seat = await _seats.GetForBookingAsync(request.SeatId, ct);
        if (seat is null)
        {
            return Result<BookingDto>.Fail(ErrorType.NotFound, "Seat not found.");
        }

        if (seat.Event.StartsAt <= DateTime.UtcNow)
        {
            return Result<BookingDto>.Fail(ErrorType.Validation, "You cannot book a seat for an event that already started.");
        }

        // Business rule: this is the "reject" path when the seat is already taken (we SAW it taken).
        if (seat.Status != SeatStatus.Available)
        {
            return Result<BookingDto>.Fail(ErrorType.Conflict, "This seat is already booked.");
        }

        seat.Status = SeatStatus.Booked;

        var booking = new Booking { UserId = userId, Seat = seat, Status = BookingStatus.Active };
        await _bookings.AddAsync(booking, ct);

        // Here the race is decided: if another user saved the same seat after we read it,
        // the Version no longer matches and the save fails -> 409 instead of a double booking.
        if (!await TrySaveAsync($"book seat {seat.Id} for user {userId}", ct))
        {
            return Result<BookingDto>.Fail(ErrorType.Conflict,
                "Someone else just booked this seat. Please choose another seat.");
        }

        return Result<BookingDto>.Success(_mapper.Map<BookingDto>(booking));
    }

    public async Task<Result> CancelAsync(int userId, bool isAdmin, int bookingId, CancellationToken ct)
    {
        var booking = await _bookings.GetForCancelAsync(bookingId, ct);
        if (booking is null)
        {
            return Result.Fail(ErrorType.NotFound, "Booking not found.");
        }

        // A customer may cancel only his own bookings. An admin may cancel any booking.
        if (!isAdmin && booking.UserId != userId)
        {
            return Result.Fail(ErrorType.Forbidden, "You can cancel only your own bookings.");
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            return Result.Fail(ErrorType.Conflict, "The booking is already cancelled.");
        }

        if (booking.Seat.Event.StartsAt <= DateTime.UtcNow)
        {
            return Result.Fail(ErrorType.Validation, "You cannot cancel a booking for an event that already started.");
        }

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;
        booking.Seat.Status = SeatStatus.Available; // the seat goes back to the pool

        if (!await TrySaveAsync($"cancel booking {bookingId}", ct))
        {
            return Result.Fail(ErrorType.Conflict, "The booking was changed by someone else. Please try again.");
        }

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<BookingDto>>> GetMineAsync(int userId, CancellationToken ct)
    {
        var bookings = await _bookings.GetByUserAsync(userId, ct);
        return Result<IReadOnlyList<BookingDto>>.Success(_mapper.Map<List<BookingDto>>(bookings));
    }

    public async Task<Result<IReadOnlyList<BookingDto>>> GetByEventAsync(int eventId, CancellationToken ct)
    {
        var bookings = await _bookings.GetByEventAsync(eventId, ct);
        return Result<IReadOnlyList<BookingDto>>.Success(_mapper.Map<List<BookingDto>>(bookings));
    }

    // Shared by Book and Cancel: saves, and turns a concurrency conflict into "false" + a Warning log.
    // The exception is NOT an unexpected error here - it is a normal outcome of the race.
    private async Task<bool> TrySaveAsync(string action, CancellationToken ct)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
            return true;
        }
        catch (ConcurrencyConflictException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict while trying to {Action}", action);
            return false;
        }
    }
}
