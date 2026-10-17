namespace HallPlaces.API.Middleware;

// Gives every request an id and puts it in the response header and in every log line of that request.
// When something goes wrong, one id lets you find ALL the log lines of that single request.
public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateId(context);

        context.Items[ItemKey] = correlationId;          // other middleware (error handler) reads it from here
        context.Response.Headers[HeaderName] = correlationId;

        // Everything logged inside this "scope" automatically carries the CorrelationId.
        using (_logger.BeginScope(new Dictionary<string, object> { [ItemKey] = correlationId }))
        {
            await _next(context);
        }
    }

    // The client may send its own id (useful when tracing a call across systems),
    // but only a short, harmless value is accepted - otherwise it could pollute the logs.
    private static string GetOrCreateId(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();

        var isValid = !string.IsNullOrWhiteSpace(incoming)
                      && incoming.Length <= 64
                      && incoming.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');

        return isValid ? incoming! : Guid.NewGuid().ToString("N");
    }
}
