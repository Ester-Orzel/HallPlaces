namespace HallPlaces.Core.Exceptions;

// Thrown by the Data layer when EF Core reports DbUpdateConcurrencyException.
// The Service layer catches THIS type, so it never needs to know EF Core exists.
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception? inner = null)
        : base(message, inner) { }
}
