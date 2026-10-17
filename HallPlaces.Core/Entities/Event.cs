namespace HallPlaces.Core.Entities;

public class Event
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    // Optional poster picture shown by the client.
    public string? ImageUrl { get; set; }

    // Always stored in UTC.
    public DateTime StartsAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Many-to-one: every event takes place in one hall.
    public int HallId { get; set; }
    public Hall Hall { get; set; } = null!;

    // One-to-many: the seats of THIS event (copied from the hall layout).
    public ICollection<Seat> Seats { get; set; } = new List<Seat>();

    // Many-to-many: an event can have several categories, a category has many events.
    public ICollection<Category> Categories { get; set; } = new List<Category>();
}
