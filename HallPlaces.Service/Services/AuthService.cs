using HallPlaces.Core.Common;
using HallPlaces.Core.DTOs;
using HallPlaces.Core.Entities;
using HallPlaces.Core.Enums;
using HallPlaces.Core.Interfaces.Repositories;
using HallPlaces.Core.Interfaces.Security;
using HallPlaces.Core.Interfaces.Services;

namespace HallPlaces.Service.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public AuthService(
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);

        if (await _users.EmailExistsAsync(email, ct))
        {
            return Result<AuthResponse>.Fail(ErrorType.Conflict, "A user with this email already exists.");
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            // Everyone who registers is a Customer. A role is NEVER taken from the request:
            // otherwise anybody could register as an Admin.
            Role = UserRole.Customer
        };

        await _users.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<AuthResponse>.Success(BuildResponse(user));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await _users.GetByEmailAsync(NormalizeEmail(request.Email), ct);

        // The SAME message for "no such user" and "wrong password",
        // so an attacker cannot find out which emails are registered.
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Result<AuthResponse>.Fail(ErrorType.Unauthorized, "Invalid email or password.");
        }

        return Result<AuthResponse>.Success(BuildResponse(user));
    }

    // Shared by Register and Login (no duplicated code).
    private AuthResponse BuildResponse(User user)
    {
        var (token, expiresAt) = _tokenGenerator.Generate(user);

        return new AuthResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role.ToString()
        };
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
