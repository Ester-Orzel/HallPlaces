using System.Diagnostics;

namespace HallPlaces.API.Middleware;

// Writes ONE log line for every request: method, path, status code and duration.
// Runs after CorrelationIdMiddleware, so the line carries the CorrelationId.
//
// Security: it logs ONLY the method and the path. Never the body (passwords on login/register),
// never the Authorization header (the JWT), never the query string.
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        catch
        {
            // The exception handler (outside us) will turn it into a 500 and log the Error with the details.
            Log(context, StatusCodes.Status500InternalServerError, stopwatch);
            throw;
        }

        Log(context, context.Response.StatusCode, stopwatch);
    }

    private void Log(HttpContext context, int statusCode, Stopwatch stopwatch)
    {
        // A client mistake (400, 404, 409...) is normal life for a server -> Information.
        // Only a server-side failure (5xx) deserves a Warning here.
        var level = statusCode >= 500 ? LogLevel.Warning : LogLevel.Information;

        _logger.Log(level, "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms",
            context.Request.Method, context.Request.Path.Value, statusCode, stopwatch.ElapsedMilliseconds);
    }
}
