using HallPlaces.Core.Common;
using HallPlaces.Core.DTOs;

namespace HallPlaces.Core.Interfaces.Services;

public interface IHallService
{
    Task<Result<IReadOnlyList<HallDto>>> GetAllAsync(CancellationToken ct);
    Task<Result<HallDto>> GetByIdAsync(int id, CancellationToken ct);
    Task<Result<HallDto>> CreateAsync(HallRequest request, CancellationToken ct);
    Task<Result<HallDto>> UpdateAsync(int id, HallRequest request, CancellationToken ct);
    Task<Result> DeleteAsync(int id, CancellationToken ct);
}
