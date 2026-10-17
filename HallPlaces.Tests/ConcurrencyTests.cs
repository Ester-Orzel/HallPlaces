using AutoMapper;
using HallPlaces.Core.Common;
using HallPlaces.Core.DTOs;
using HallPlaces.Core.Entities;
using HallPlaces.Core.Enums;
using HallPlaces.Core.Exceptions;
using HallPlaces.Data;
using HallPlaces.Data.Repositories;
using HallPlaces.Service.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HallPlaces.Tests;

// Part C of the project: optimistic concurrency, tested against a REAL database engine
// (SQLite in memory), with TWO SEPARATE DbContexts = two users at the same moment.
public class ConcurrencyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private int _seatId;

    public ConcurrencyTests()
    {
        // An in-memory SQLite database lives as long as its connection is open.
        // Both DbContexts use THIS connection, so they see the same data.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var context = new AppDbContext(_options);
        context.Database.EnsureCreated();
        SeedOneSeat(context);
    }

    private void SeedOneSeat(AppDbContext context)
    {
        var hall = new Hall { Name = "Test hall", Address = "Test street", RowSeatCounts = new List<int> { 1 } };
        var ev = new Event { Title = "Test event", StartsAt = DateTime.UtcNow.AddDays(7), Hall = hall };
        var seat = new Seat { Event = ev, RowNumber = 1, SeatNumber = 1, Price = 50m };

        context.Users.AddRange(
            new User { FullName = "First", Email = "first@test.com", PasswordHash = "x" },
            new User { FullName = "Second", Email = "second@test.com", PasswordHash = "x" });
        context.Seats.Add(seat);
        context.SaveChanges();

        _seatId = seat.Id;
    }

    [Fact]
    public async Task TwoContexts_BothReadTheSeat_BothModifyIt_TheSecondSaveFails()
    {
        await using var context1 = new AppDbContext(_options);
        await using var context2 = new AppDbContext(_options);

        // 1. BOTH users read the same seat. For both of them it is Available.
        var seatOfUser1 = await new SeatRepository(context1).GetForBookingAsync(_seatId, CancellationToken.None);
        var seatOfUser2 = await new SeatRepository(context2).GetForBookingAsync(_seatId, CancellationToken.None);
        Assert.Equal(SeatStatus.Available, seatOfUser1!.Status);
        Assert.Equal(SeatStatus.Available, seatOfUser2!.Status);

        // 2. BOTH modify it.
        seatOfUser1.Status = SeatStatus.Booked;
        seatOfUser2.Status = SeatStatus.Booked;

        // 3. The first saves: succeeds.
        await new UnitOfWork(context1).SaveChangesAsync(CancellationToken.None);

        // 4. The second saves: its Version is already old -> the specific exception.
        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => new UnitOfWork(context2).SaveChangesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task TheSameSeatCannotBeBookedTwice_ThroughBookingService()
    {
        var first = await BookWithNewContextAsync(userId: 1);
        var second = await BookWithNewContextAsync(userId: 2);

        Assert.True(first.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.Equal(ErrorType.Conflict, second.ErrorType);

        // And the database holds exactly ONE active booking for the seat.
        await using var check = new AppDbContext(_options);
        Assert.Equal(1, await check.Bookings.CountAsync(b => b.SeatId == _seatId && b.Status == BookingStatus.Active));
    }

    // Each call = one HTTP request = a NEW DbContext (Scoped), like in the real application.
    private async Task<Result<BookingDto>> BookWithNewContextAsync(int userId)
    {
        await using var context = new AppDbContext(_options);

        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<BookingDto>(It.IsAny<object>())).Returns(new BookingDto());

        var service = new BookingService(
            new SeatRepository(context),
            new BookingRepository(context),
            new UnitOfWork(context),
            mapper.Object,
            NullLogger<BookingService>.Instance);

        return await service.BookAsync(userId, new BookingRequest { SeatId = _seatId }, CancellationToken.None);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
