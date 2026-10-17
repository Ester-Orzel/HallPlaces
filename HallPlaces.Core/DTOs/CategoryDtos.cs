using System.ComponentModel.DataAnnotations;

namespace HallPlaces.Core.DTOs;

public class CategoryRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;
}

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
