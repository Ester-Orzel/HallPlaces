using HallPlaces.Core.DTOs;
using HallPlaces.Core.Enums;
using HallPlaces.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HallPlaces.API.Controllers;

[Route("api")]
[Authorize]   // every action here needs a logged-in user (any role)
public class BookingsController : ApiControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    // POST /api/bookings   body: { "seatId": 17 }
    // 201 = booked | 409 = the seat is taken (or someone just took it) | 404 = no such seat
    [HttpPost("bookings")]
    public async Task<ActionResult<BookingDto>> Book([FromBody] BookingRequest request, CancellationToken ct)
    {
        // The user comes from the TOKEN. A client can never book "as someone else".
        var result = await _bookingService.BookAsync(CurrentUserId, request, ct);

        // There is no "get one booking" endpoint, so Location points to "my bookings".
        return ToCreated(result, nameof(GetMine), _ => null);
    }

    // GET /api/bookings/mine
    [HttpGet("bookings/mine")]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> GetMine(CancellationToken ct)
    {
        return ToActionResult(await _bookingService.GetMineAsync(CurrentUserId, ct));
    }

    // DELETE /api/bookings/12   -> 204. A customer: only his own. An admin: any.
    [HttpDelete("bookings/{id:int}")]
    public async Task<IActionResult> Cancel([FromRoute] int id, CancellationToken ct)
    {
        return ToNoContent(await _bookingService.CancelAsync(CurrentUserId, IsAdmin, id, ct));
    }

    // GET /api/events/5/bookings   (Admin only)
    [HttpGet("events/{eventId:int}/bookings")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> GetByEvent([FromRoute] int eventId, CancellationToken ct)
    {
        return ToActionResult(await _bookingService.GetByEventAsync(eventId, ct));
    }
}
