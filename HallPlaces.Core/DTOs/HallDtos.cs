using System.ComponentModel.DataAnnotations;

namespace HallPlaces.Core.DTOs;

public class HallRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(250)]
    public string Address { get; set; } = string.Empty;

    // The number of seats in each row: [10, 12, 14] = 3 rows. The rows may differ in length.
    [Required, MinLength(1), MaxLength(100)]
    public List<int> RowSeatCounts { get; set; } = new();
}

public class HallDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public List<int> RowSeatCounts { get; set; } = new();
}
