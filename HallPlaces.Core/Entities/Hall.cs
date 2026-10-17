namespace HallPlaces.Core.Entities;

// A physical hall. It is a TEMPLATE: when an event is created,
// seats are copied from this layout into the event.
public class Hall
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    // Seats per row. [10, 12, 14] = row 1 has 10 seats, row 2 has 12, row 3 has 14.
    // EF Core stores this as a single column (a PostgreSQL integer array).
    public List<int> RowSeatCounts { get; set; } = new();

    // One-to-many: one hall hosts many events.
    public ICollection<Event> Events { get; set; } = new List<Event>();
}
