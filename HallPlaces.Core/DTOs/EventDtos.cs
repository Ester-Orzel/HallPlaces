using System.ComponentModel.DataAnnotations;

namespace HallPlaces.Core.DTOs;

public class EventRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Url, StringLength(500)]
    public string? ImageUrl { get; set; }

    // Must be UTC and in the future (the "in the future" part is a business rule, checked in the Service).
    [Required]
    public DateTime StartsAt { get; set; }

    [Range(1, int.MaxValue)]
    public int HallId { get; set; }

    // The BASE price = the price of the back rows. Rows closer to the stage cost more (see EventService.PriceForRow).
    [Range(0.01, 100000)]
    public decimal SeatPrice { get; set; }

    public List<int> CategoryIds { get; set; } = new();
}

// Used both for the paged list and for the single-event details.
public class EventDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime StartsAt { get; set; }
    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public List<CategoryDto> Categories { get; set; } = new();
}

public class SeatDto
{
    public int Id { get; set; }
    public int RowNumber { get; set; }
    public int SeatNumber { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
}
