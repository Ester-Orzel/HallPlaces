using HallPlaces.Core.DTOs;
using HallPlaces.Core.Enums;
using HallPlaces.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HallPlaces.API.Controllers;

[Route("api/categories")]
public class CategoriesController : ApiControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    // GET /api/categories   (public)
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll(CancellationToken ct)
    {
        return ToActionResult(await _categoryService.GetAllAsync(ct));
    }

    // POST /api/categories   (Admin only)
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<CategoryDto>> Create([FromBody] CategoryRequest request, CancellationToken ct)
    {
        // There is no "get one category" endpoint, so Location points to the list.
        return ToCreated(await _categoryService.CreateAsync(request, ct), nameof(GetAll), _ => null);
    }
}
