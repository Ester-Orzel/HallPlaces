using HallPlaces.Core.Common;
using HallPlaces.Core.DTOs;

namespace HallPlaces.Core.Interfaces.Services;

public interface ICategoryService
{
    Task<Result<IReadOnlyList<CategoryDto>>> GetAllAsync(CancellationToken ct);
    Task<Result<CategoryDto>> CreateAsync(CategoryRequest request, CancellationToken ct);
}
