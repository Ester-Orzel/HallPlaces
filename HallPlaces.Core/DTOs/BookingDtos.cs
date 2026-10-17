using System.ComponentModel.DataAnnotations;

namespace HallPlaces.Core.DTOs;

public class BookingRequest
{
    [Range(1, int.MaxValue)]
    public int SeatId { get; set; }
}

public class BookingDto
{
    public int Id { get; set; }
    public int SeatId { get; set; }
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public DateTime EventStartsAt { get; set; }
    public int RowNumber { get; set; }
    public int SeatNumber { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    // Filled only in the admin view (the user is not loaded in "my bookings").
    public string? UserFullName { get; set; }
}
