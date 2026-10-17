using HallPlaces.Core.DTOs;
using HallPlaces.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HallPlaces.API.Controllers;

[Route("api/auth")]
[AllowAnonymous]   // you cannot be logged in before you register / log in
public class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        return ToActionResult(await _authService.RegisterAsync(request, ct));
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        return ToActionResult(await _authService.LoginAsync(request, ct));
    }
}
