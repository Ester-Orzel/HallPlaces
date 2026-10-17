using HallPlaces.Core.DTOs;
using HallPlaces.Core.Enums;
using HallPlaces.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HallPlaces.API.Controllers;

[Route("api/halls")]
[Authorize(Roles = nameof(UserRole.Admin))]   // the whole controller: Admin only
public class HallsController : ApiControllerBase
{
    private readonly IHallService _hallService;

    public HallsController(IHallService hallService)
    {
        _hallService = hallService;
    }

    // GET /api/halls
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HallDto>>> GetAll(CancellationToken ct)
    {
        return ToActionResult(await _hallService.GetAllAsync(ct));
    }

    // GET /api/halls/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<HallDto>> GetById([FromRoute] int id, CancellationToken ct)
    {
        return ToActionResult(await _hallService.GetByIdAsync(id, ct));
    }

    // POST /api/halls   -> 201 + Location: /api/halls/5
    [HttpPost]
    public async Task<ActionResult<HallDto>> Create([FromBody] HallRequest request, CancellationToken ct)
    {
        return ToCreated(await _hallService.CreateAsync(request, ct), nameof(GetById), hall => new { id = hall.Id });
    }

    // PUT /api/halls/5
    [HttpPut("{id:int}")]
    public async Task<ActionResult<HallDto>> Update([FromRoute] int id, [FromBody] HallRequest request, CancellationToken ct)
    {
        return ToActionResult(await _hallService.UpdateAsync(id, request, ct));
    }

    // DELETE /api/halls/5   -> 204
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken ct)
    {
        return ToNoContent(await _hallService.DeleteAsync(id, ct));
    }
}
