using System.ComponentModel.DataAnnotations;
using HallPlaces.Core.Enums;

namespace HallPlaces.Core.Entities;

// THE LIMITED RESOURCE: a specific seat in a specific event.
// Created from the hall layout when the event is created (a frozen snapshot),
// so later changes to the hall never affect seats that were already sold.
public class Seat
{
    public int Id { get; set; }

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public int RowNumber { get; set; }
    public int SeatNumber { get; set; }

    public decimal Price { get; set; }

    // Deliberately stored on the seat (and not only derived from Bookings):
    // booking a seat must UPDATE this row, so that Version detects two
    // users booking the same seat at the same time.
    public SeatStatus Status { get; set; } = SeatStatus.Available;

    // Concurrency token. AppDbContext gives it a new value on every update.
    // EF Core adds the OLD value to the WHERE clause; if someone changed the row
    // in the meantime, no row is updated and EF throws DbUpdateConcurrencyException.
    [ConcurrencyCheck]
    public Guid Version { get; set; } = Guid.NewGuid();

    // History: a seat can be booked, cancelled and booked again.
    // At most ONE booking per seat is Active (also enforced by a unique index in Data).
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
