using AutoMapper;
using HallPlaces.Core.Common;
using HallPlaces.Core.DTOs;
using HallPlaces.Core.Entities;
using HallPlaces.Core.Interfaces.Repositories;
using HallPlaces.Core.Interfaces.Services;

namespace HallPlaces.Service.Services;

public class HallService : IHallService
{
    private const int MaxSeatsPerRow = 100;
    private const int MaxSeatsInHall = 2000;

    private readonly IHallRepository _halls;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public HallService(IHallRepository halls, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _halls = halls;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<HallDto>>> GetAllAsync(CancellationToken ct)
    {
        var halls = await _halls.GetAllAsync(ct);
        return Result<IReadOnlyList<HallDto>>.Success(_mapper.Map<List<HallDto>>(halls));
    }

    public async Task<Result<HallDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var hall = await _halls.GetByIdAsync(id, ct);
        return hall is null
            ? Result<HallDto>.Fail(ErrorType.NotFound, "Hall not found.")
            : Result<HallDto>.Success(_mapper.Map<HallDto>(hall));
    }

    public async Task<Result<HallDto>> CreateAsync(HallRequest request, CancellationToken ct)
    {
        var error = ValidateLayout(request.RowSeatCounts);
        if (error is not null)
        {
            return Result<HallDto>.Fail(ErrorType.Validation, error);
        }

        var hall = _mapper.Map<Hall>(request);

        await _halls.AddAsync(hall, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<HallDto>.Success(_mapper.Map<HallDto>(hall));
    }

    public async Task<Result<HallDto>> UpdateAsync(int id, HallRequest request, CancellationToken ct)
    {
        var error = ValidateLayout(request.RowSeatCounts);
        if (error is not null)
        {
            return Result<HallDto>.Fail(ErrorType.Validation, error);
        }

        var hall = await _halls.GetByIdAsync(id, ct);
        if (hall is null)
        {
            return Result<HallDto>.Fail(ErrorType.NotFound, "Hall not found.");
        }

        // Existing events are NOT affected: their seats were copied when the event was created.
        _mapper.Map(request, hall);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<HallDto>.Success(_mapper.Map<HallDto>(hall));
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct)
    {
        var hall = await _halls.GetByIdAsync(id, ct);
        if (hall is null)
        {
            return Result.Fail(ErrorType.NotFound, "Hall not found.");
        }

        if (await _halls.HasEventsAsync(id, ct))
        {
            return Result.Fail(ErrorType.Conflict, "The hall has events and cannot be deleted.");
        }

        _halls.Remove(hall);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    // Business validation: Data Annotations can check "the list is not empty",
    // but not "every row is between 1 and 100 and the hall is not huge".
    private static string? ValidateLayout(List<int> rowSeatCounts)
    {
        if (rowSeatCounts.Any(count => count < 1 || count > MaxSeatsPerRow))
        {
            return $"Every row must have between 1 and {MaxSeatsPerRow} seats.";
        }

        if (rowSeatCounts.Sum() > MaxSeatsInHall)
        {
            return $"A hall cannot have more than {MaxSeatsInHall} seats.";
        }

        return null;
    }
}
