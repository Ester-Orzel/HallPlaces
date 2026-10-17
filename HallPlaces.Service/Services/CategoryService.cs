using AutoMapper;
using HallPlaces.Core.Common;
using HallPlaces.Core.DTOs;
using HallPlaces.Core.Entities;
using HallPlaces.Core.Interfaces.Repositories;
using HallPlaces.Core.Interfaces.Services;

namespace HallPlaces.Service.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    // Dependency Injection: we ask for INTERFACES; Program.cs decides which class we get.
    public CategoryService(ICategoryRepository categories, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<CategoryDto>>> GetAllAsync(CancellationToken ct)
    {
        var categories = await _categories.GetAllAsync(ct);
        return Result<IReadOnlyList<CategoryDto>>.Success(_mapper.Map<List<CategoryDto>>(categories));
    }

    public async Task<Result<CategoryDto>> CreateAsync(CategoryRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();

        // Business rule (not a Data Annotation): category names are unique.
        if (await _categories.NameExistsAsync(name, ct))
        {
            return Result<CategoryDto>.Fail(ErrorType.Conflict, $"A category named '{name}' already exists.");
        }

        var category = _mapper.Map<Category>(request);
        category.Name = name;

        await _categories.AddAsync(category, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<CategoryDto>.Success(_mapper.Map<CategoryDto>(category));
    }
}
