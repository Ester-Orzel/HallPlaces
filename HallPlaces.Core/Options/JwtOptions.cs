namespace HallPlaces.Core.Options;

// The "Jwt" section of the configuration. Issuer/Audience/ExpiryMinutes live in appsettings.json.
// The Key is a SECRET: it exists only in User Secrets (dev) / environment variables (Render).
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
    public string Key { get; set; } = string.Empty;
}
