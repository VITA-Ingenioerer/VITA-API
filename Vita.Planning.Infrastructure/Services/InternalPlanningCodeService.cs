using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class InternalPlanningCodeService : IInternalPlanningCodeService
{
    private static readonly HashSet<string> AllowedCategories =
    [
        "Internal",
        "Absence",
        "Sales",
        "SmallJobs",
        "Training",
        "Other"
    ];

    private readonly PlanningDbContext _dbContext;

    public InternalPlanningCodeService(PlanningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<InternalPlanningCodeDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.InternalPlanningCodes
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<InternalPlanningCodeDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.InternalPlanningCodes
            .AsNoTracking()
            .Where(x => x.InternalPlanningCodeId == id)
            .Select(MapToDtoExpression())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<InternalPlanningCodeDto> CreateAsync(
        CreateInternalPlanningCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCategory(request.Category);

        var normalizedCode = request.Code.Trim().ToUpperInvariant();

        var exists = await _dbContext.InternalPlanningCodes
            .AnyAsync(x => x.Code == normalizedCode, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"Internal planning code '{normalizedCode}' already exists.");
        }

        var entity = new InternalPlanningCode
        {
            Code = normalizedCode,
            Name = request.Name.Trim(),
            Category = request.Category.Trim(),
            OfficeCode = NormalizeNullable(request.OfficeCode),
            DefaultDescription = NormalizeNullable(request.DefaultDescription),
            ColorTag = NormalizeNullable(request.ColorTag),
            IsActive = request.IsActive,
            IsPlannable = request.IsPlannable,
            IsAbsence = request.IsAbsence,
            IsInternal = request.IsInternal,
            IsBillable = request.IsBillable,
            CreatedBy = NormalizeNullable(request.CreatedBy),
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.InternalPlanningCodes.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    public async Task<InternalPlanningCodeDto?> UpdateAsync(
        int id,
        UpdateInternalPlanningCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCategory(request.Category);

        var entity = await _dbContext.InternalPlanningCodes
            .FirstOrDefaultAsync(x => x.InternalPlanningCodeId == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        entity.Name = request.Name.Trim();
        entity.Category = request.Category.Trim();
        entity.OfficeCode = NormalizeNullable(request.OfficeCode);
        entity.DefaultDescription = NormalizeNullable(request.DefaultDescription);
        entity.ColorTag = NormalizeNullable(request.ColorTag);
        entity.IsActive = request.IsActive;
        entity.IsPlannable = request.IsPlannable;
        entity.IsAbsence = request.IsAbsence;
        entity.IsInternal = request.IsInternal;
        entity.IsBillable = request.IsBillable;
        entity.UpdatedBy = NormalizeNullable(request.UpdatedBy);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    private static void ValidateCategory(string category)
    {
        if (!AllowedCategories.Contains(category.Trim()))
        {
            throw new InvalidOperationException(
                $"Category '{category}' is invalid. Allowed values: {string.Join(", ", AllowedCategories)}.");
        }
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static InternalPlanningCodeDto MapToDto(InternalPlanningCode entity)
    {
        return new InternalPlanningCodeDto
        {
            InternalPlanningCodeId = entity.InternalPlanningCodeId,
            Code = entity.Code,
            Name = entity.Name,
            Category = entity.Category,
            OfficeCode = entity.OfficeCode,
            DefaultDescription = entity.DefaultDescription,
            ColorTag = entity.ColorTag,
            IsActive = entity.IsActive,
            IsPlannable = entity.IsPlannable,
            IsAbsence = entity.IsAbsence,
            IsInternal = entity.IsInternal,
            IsBillable = entity.IsBillable,
            CreatedBy = entity.CreatedBy,
            UpdatedBy = entity.UpdatedBy,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    private static System.Linq.Expressions.Expression<Func<InternalPlanningCode, InternalPlanningCodeDto>> MapToDtoExpression()
    {
        return entity => new InternalPlanningCodeDto
        {
            InternalPlanningCodeId = entity.InternalPlanningCodeId,
            Code = entity.Code,
            Name = entity.Name,
            Category = entity.Category,
            OfficeCode = entity.OfficeCode,
            DefaultDescription = entity.DefaultDescription,
            ColorTag = entity.ColorTag,
            IsActive = entity.IsActive,
            IsPlannable = entity.IsPlannable,
            IsAbsence = entity.IsAbsence,
            IsInternal = entity.IsInternal,
            IsBillable = entity.IsBillable,
            CreatedBy = entity.CreatedBy,
            UpdatedBy = entity.UpdatedBy,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }
}