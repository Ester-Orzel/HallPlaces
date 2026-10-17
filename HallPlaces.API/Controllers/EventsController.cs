using HallPlaces.Core.Common;
using HallPlaces.Core.DTOs;
using HallPlaces.Core.Enums;
using HallPlaces.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HallPlaces.API.Controllers;

[Route("api/events")]
public class EventsController : ApiControllerBase
{
    private readonly IEventService _eventService;

    public EventsController(IEventService eventService)
    {
        _eventService = eventService;
    }

    // GET /api/events?page=2&pageSize=10&search=rock&categoryId=3&sortBy=Title&descending=true   (public)
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<EventDto>>> GetPaged([FromQuery] EventQuery query, CancellationToken ct)
    {
        return ToActionResult(await _eventService.GetPagedAsync(query, ct));
    }

    // GET /api/events/5   (public)
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<EventDto>> GetById([FromRoute] int id, CancellationToken ct)
    {
        return ToActionResult(await _eventService.GetByIdAsync(id, ct));
    }

    // GET /api/events/5/seats   (public: the seat map)
    [HttpGet("{eventId:int}/seats")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<SeatDto>>> GetSeats([FromRoute] int eventId, CancellationToken ct)
    {
        return ToActionResult(await _eventService.GetSeatsAsync(eventId, ct));
    }

    // POST /api/events   (Admin)
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<EventDto>> Create([FromBody] EventRequest request, CancellationToken ct)
    {
        return ToCreated(await _eventService.CreateAsync(request, ct), nameof(GetById), ev => new { id = ev.Id });
    }

    // PUT /api/events/5   (Admin)
    [HttpPut("{id:int}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<EventDto>> Update([FromRoute] int id, [FromBody] EventRequest request, CancellationToken ct)
    {
        return ToActionResult(await _eventService.UpdateAsync(id, request, ct));
    }

    // DELETE /api/events/5   (Admin) -> 204
    [HttpDelete("{id:int}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken ct)
    {
        return ToNoContent(await _eventService.DeleteAsync(id, ct));
    }
}
