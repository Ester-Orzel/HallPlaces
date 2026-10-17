using HallPlaces.Core.Common;
using HallPlaces.Core.DTOs;

namespace HallPlaces.Core.Interfaces.Services;

public interface IBookingService
{
    Task<Result<BookingDto>> BookAsync(int userId, BookingRequest request, CancellationToken ct);
    Task<Result> CancelAsync(int userId, bool isAdmin, int bookingId, CancellationToken ct);
    Task<Result<IReadOnlyList<BookingDto>>> GetMineAsync(int userId, CancellationToken ct);
    Task<Result<IReadOnlyList<BookingDto>>> GetByEventAsync(int eventId, CancellationToken ct);
}
