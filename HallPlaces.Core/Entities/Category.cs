namespace HallPlaces.Core.Entities;

public class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    // Many-to-many with Event (the join table is created by EF Core).
    public ICollection<Event> Events { get; set; } = new List<Event>();
}
