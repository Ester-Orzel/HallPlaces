using HallPlaces.Core.Common;
using HallPlaces.Core.DTOs;

namespace HallPlaces.Core.Interfaces.Services;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct);
}
