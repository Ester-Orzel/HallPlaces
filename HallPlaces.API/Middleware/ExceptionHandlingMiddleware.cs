using Microsoft.AspNetCore.Mvc;

namespace HallPlaces.API.Middleware;

// The safety net for UNEXPECTED errors (a bug, the database is down...).
// It must be the FIRST middleware, so that it wraps everything that comes after it.
// Expected failures ("seat taken", "not found") never get here: Services return a Result for them.
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client closed the connection / navigated away. Not an error of ours.
            _logger.LogInformation("The client cancelled the request {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = 499;
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            // The FULL exception (with stack trace) goes to the log only.
            _logger.LogError(ex, "Unhandled exception while handling {Method} {Path}", context.Request.Method, context.Request.Path);

            await WriteErrorResponseAsync(context);
        }
    }

    // The client gets a uniform JSON and NEVER the details of the exception (that would leak internals).
    private static async Task WriteErrorResponseAsync(HttpContext context)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = "Something went wrong on the server. Please try again, and quote the correlationId if the problem remains."
        };
        problem.Extensions["correlationId"] = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString();

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
