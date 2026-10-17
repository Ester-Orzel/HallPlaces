using HallPlaces.Core.Interfaces.Security;

namespace HallPlaces.Service.Security;

// BCrypt: a slow one-way hash with a random salt built in.
// Two users with the same password get different hashes, and the password cannot be recovered.
public class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string password, string passwordHash) => BCrypt.Net.BCrypt.Verify(password, passwordHash);
}
