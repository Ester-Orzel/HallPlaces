using System.Security.Claims;
using HallPlaces.Core.Common;
using HallPlaces.Core.Enums;
using Microsoft.AspNetCore.Mvc;

namespace HallPlaces.API.Controllers;

// Base class of all controllers. It holds the code that every controller needs,
// so no controller repeats it: turning a Service's Result into an HTTP response.
[ApiController]   // automatic 400 when Data Annotations validation fails + binding rules
public abstract class ApiControllerBase : ControllerBase
{
    // Read from the claims inside the JWT (set by JwtTokenGenerator), never from the request body.
    protected int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected bool IsAdmin => User.IsInRole(nameof(UserRole.Admin));

    // Success -> 200 + the DTO.
    protected ActionResult<T> ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Failure(result);
    }

    // Success -> 201 Created + a Location header (where the new resource can be read).
    protected ActionResult<T> ToCreated<T>(Result<T> result, string actionName, Func<T, object?> routeValues)
    {
        if (result.IsSuccess)
        {
            return CreatedAtAction(actionName, routeValues(result.Value!), result.Value);
        }

        return Failure(result);
    }

    // Success -> 204 No Content.
    protected IActionResult ToNoContent(Result result)
    {
        if (result.IsSuccess)
        {
            return NoContent();
        }

        return Failure(result);
    }

    // One place that maps "what went wrong" to the HTTP status code.
    // Problem() produces a standard JSON body (ProblemDetails).
    private ObjectResult Failure(Result result)
    {
        var statusCode = result.ErrorType switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        return Problem(detail: result.Error, statusCode: statusCode);
    }
}
