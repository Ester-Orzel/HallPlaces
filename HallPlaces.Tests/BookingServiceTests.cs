using AutoMapper;
using HallPlaces.Core.Common;
using HallPlaces.Core.DTOs;
using HallPlaces.Core.Entities;
using HallPlaces.Core.Enums;
using HallPlaces.Core.Exceptions;
using HallPlaces.Core.Interfaces.Repositories;
using HallPlaces.Service.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HallPlaces.Tests;

// Unit tests of the BUSINESS LOGIC of BookingService.
// The repositories are fake (Moq): no database, so the tests are fast and test ONLY the Service.
public class BookingServiceTests
{
    private const int UserId = 1;

    private readonly Mock<ISeatRepository> _seats = new();
    private readonly Mock<IBookingRepository> _bookings = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IMapper> _mapper = new();

    private BookingService CreateService()
    {
        // The mapper is not what we test here: it just returns an empty DTO.
        _mapper.Setup(m => m.Map<BookingDto>(It.IsAny<object>())).Returns(new BookingDto());

        return new BookingService(
            _seats.Object, _bookings.Object, _unitOfWork.Object, _mapper.Object,
            NullLogger<BookingService>.Instance);
    }

    private static Seat CreateSeat(SeatStatus status, bool eventInFuture = true)
    {
        return new Seat
        {
            Id = 10,
            RowNumber = 1,
            SeatNumber = 1,
            Price = 50m,
            Status = status,
            Event = new Event
            {
                Id = 5,
                Title = "Test event",
                StartsAt = eventInFuture ? DateTime.UtcNow.AddDays(7) : DateTime.UtcNow.AddDays(-1)
            }
        };
    }

    private void SeatExists(Seat seat)
    {
        _seats.Setup(r => r.GetForBookingAsync(seat.Id, It.IsAny<CancellationToken>())).ReturnsAsync(seat);
    }

    // ---------- Book: the limited resource ----------

    [Fact]
    public async Task Book_AvailableSeat_IsAccepted_AndTheSeatBecomesBooked()
    {
        var seat = CreateSeat(SeatStatus.Available);
        SeatExists(seat);

        var result = await CreateService().BookAsync(UserId, new BookingRequest { SeatId = seat.Id }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SeatStatus.Booked, seat.Status);
        _bookings.Verify(r => r.AddAsync(
            It.Is<Booking>(b => b.UserId == UserId && b.Status == BookingStatus.Active),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Book_AlreadyBookedSeat_IsRejectedWithConflict_AndNothingIsSaved()
    {
        var seat = CreateSeat(SeatStatus.Booked);
        SeatExists(seat);

        var result = await CreateService().BookAsync(UserId, new BookingRequest { SeatId = seat.Id }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        _bookings.Verify(r => r.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Book_WhenSomeoneElseSavedFirst_IsRejectedWithConflict()
    {
        // The seat LOOKED available, but the database refuses the save (the Version changed meanwhile).
        var seat = CreateSeat(SeatStatus.Available);
        SeatExists(seat);
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("someone else booked it"));

        var result = await CreateService().BookAsync(UserId, new BookingRequest { SeatId = seat.Id }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task Book_UnknownSeat_ReturnsNotFound()
    {
        _seats.Setup(r => r.GetForBookingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((Seat?)null);

        var result = await CreateService().BookAsync(UserId, new BookingRequest { SeatId = 999 }, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task Book_SeatOfAnEventThatAlreadyStarted_ReturnsValidationError()
    {
        var seat = CreateSeat(SeatStatus.Available, eventInFuture: false);
        SeatExists(seat);

        var result = await CreateService().BookAsync(UserId, new BookingRequest { SeatId = seat.Id }, CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    // ---------- Cancel: who may cancel ----------

    private Booking CreateBookingOfAnotherUser()
    {
        var booking = new Booking
        {
            Id = 20,
            UserId = 2,                         // NOT the user who tries to cancel
            Status = BookingStatus.Active,
            Seat = CreateSeat(SeatStatus.Booked)
        };
        _bookings.Setup(r => r.GetForCancelAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        return booking;
    }

    [Fact]
    public async Task Cancel_BookingOfAnotherUser_ByACustomer_IsForbidden()
    {
        var booking = CreateBookingOfAnotherUser();

        var result = await CreateService().CancelAsync(UserId, isAdmin: false, booking.Id, CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
        Assert.Equal(BookingStatus.Active, booking.Status);
    }

    [Fact]
    public async Task Cancel_BookingOfAnotherUser_ByAnAdmin_IsAllowed_AndTheSeatIsFreed()
    {
        var booking = CreateBookingOfAnotherUser();

        var result = await CreateService().CancelAsync(UserId, isAdmin: true, booking.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(SeatStatus.Available, booking.Seat.Status);
        Assert.NotNull(booking.CancelledAt);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelledBooking_ReturnsConflict()
    {
        var booking = CreateBookingOfAnotherUser();
        booking.Status = BookingStatus.Cancelled;

        var result = await CreateService().CancelAsync(UserId, isAdmin: true, booking.Id, CancellationToken.None);

        Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }
}
