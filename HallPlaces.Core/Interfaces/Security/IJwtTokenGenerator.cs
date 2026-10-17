using HallPlaces.Core.Entities;

namespace HallPlaces.Core.Interfaces.Security;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAt) Generate(User user);
}
