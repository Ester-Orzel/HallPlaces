using System.Text;
using HallPlaces.API.Middleware;
using HallPlaces.API.OpenApi;
using HallPlaces.Core.Interfaces.Repositories;
using HallPlaces.Core.Interfaces.Security;
using HallPlaces.Core.Options;
using HallPlaces.Core.Interfaces.Services;
using HallPlaces.Data;
using HallPlaces.Data.Repositories;
using HallPlaces.Service.Mapping;
using HallPlaces.Service.Security;
using HallPlaces.Service.Seed;
using HallPlaces.Service.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

// ---------- Logging ----------
// Replace the default logger with NLog. The rest of the code still uses ILogger<T> (no change needed there);
// NLog (configured in nlog.config) decides where the lines go: console + file.
builder.Logging.ClearProviders();
builder.Host.UseNLog();

// ---------- Services (Dependency Injection) ----------

var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'Default' is missing. Set it with User Secrets (see README).");
}

// Scoped = one instance per HTTP request. All the repositories of a request
// share the same DbContext, so ONE SaveChanges saves all of their changes together.
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// "Whoever asks for the interface (Core) gets the implementation (Data)."
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IHallRepository, HallRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<ISeatRepository, SeatRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Business logic. Controllers will ask for the interface and get the implementation.
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IHallService, HallService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IBookingService, BookingService>();

// AutoMapper scans the assembly of MappingProfile and finds all the Profiles in it.
builder.Services.AddAutoMapper(cfg => cfg.AddMaps(typeof(MappingProfile).Assembly));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<DataSeeder>();
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

// ---------- JWT ----------
// IOptions<JwtOptions> is filled from the "Jwt" section (appsettings + User Secrets / environment variables).
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key is missing or shorter than 32 characters. Set it with User Secrets (see README).");
}

// How the server CHECKS an incoming token: signature, issuer, audience, expiry.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

// ---------- CORS ----------
// The React client runs on ANOTHER address than the API. The browser blocks such calls
// unless the API says "this origin is allowed". The addresses come from configuration
// (Render: the environment variable Cors__AllowedOrigins__0 = the address of the deployed client).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (builder.Environment.IsDevelopment())
    {
        // While developing, any port on this computer is fine (Vite moves to 5174, 5175... when 5173 is busy).
        policy.SetIsOriginAllowed(origin => new Uri(origin).IsLoopback);
    }
    else
    {
        // In production ONLY the listed client addresses are allowed.
        policy.WithOrigins(allowedOrigins);
    }

    policy.AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddControllers();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();

// ---------- Database: migrate + seed on startup ----------
// The system must run right after "clone + run": no manual database step.
// MigrateAsync creates the database (if needed) and applies every migration that was not applied yet.
// The same code runs on Render, where nobody can type commands.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync(CancellationToken.None);
}

// ---------- HTTP pipeline (order matters) ----------

// 1st: the global error handler wraps EVERYTHING after it, so no exception can escape it.
app.UseMiddleware<ExceptionHandlingMiddleware>();
// 2nd: every request gets a CorrelationId before anything else runs.
app.UseMiddleware<CorrelationIdMiddleware>();
// 3rd: one log line per request (it needs the CorrelationId, so it comes after it).
app.UseMiddleware<RequestLoggingMiddleware>();

// The API description (JSON) is produced by ASP.NET Core; Swagger UI is the page that displays it.
// Available also on Render, so the deployed API can be tried from the browser.
app.MapOpenApi();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "HallPlaces API"));

// Locally the client talks to plain http://localhost:5082. A redirect to https would break its calls
// (the browser does not follow a redirect on a CORS "preflight" request, and the dev certificate is not trusted).
// On Render the https is handled by Render itself, so the redirect is only kept outside Development.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// CORS must come before authentication, so that even the browser's "preflight" requests are answered.
app.UseCors();

// Authentication (who are you?) MUST come before Authorization (are you allowed?).
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
