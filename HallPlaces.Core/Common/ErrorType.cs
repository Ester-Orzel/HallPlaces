namespace HallPlaces.Core.Common;

// The kinds of failure a Service can report.
// The API maps each one to an HTTP status code (Validation -> 400, NotFound -> 404, ...).
public enum ErrorType
{
    None = 0,
    Validation = 1,
    Unauthorized = 2,
    Forbidden = 3,
    NotFound = 4,
    Conflict = 5
}
