using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class EmployeeCapacityProfileService : IEmployeeCapacityProfileService
{
    private readonly PlanningDbContext _dbContext;

    public EmployeeCapacityProfileService(PlanningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<EmployeeCapacityProfileDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.EmployeeCapacityProfiles
            .AsNoTracking()
            .OrderBy(x => x.EmployeeId)
            .ThenBy(x => x.EffectiveFrom)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeCapacityProfileDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.EmployeeCapacityProfiles
            .AsNoTracking()
            .Where(x => x.EmployeeCapacityProfileId == id)
            .Select(MapToDtoExpression())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<EmployeeCapacityProfileDto> CreateAsync(CreateEmployeeCapacityProfileRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new EmployeeCapacityProfile
        {
            EmployeeId = request.EmployeeId,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            DefaultWeeklyHours = request.DefaultWeeklyHours,
            IsActive = request.IsActive,
            MondayHours = request.MondayHours,
            TuesdayHours = request.TuesdayHours,
            WednesdayHours = request.WednesdayHours,
            ThursdayHours = request.ThursdayHours,
            FridayHours = request.FridayHours,
            SaturdayHours = request.SaturdayHours,
            SundayHours = request.SundayHours,
            CreatedBy = NormalizeNullable(request.CreatedBy),
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.EmployeeCapacityProfiles.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    public async Task<EmployeeCapacityProfileDto?> UpdateAsync(int id, UpdateEmployeeCapacityProfileRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.EmployeeCapacityProfiles
            .FirstOrDefaultAsync(x => x.EmployeeCapacityProfileId == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        entity.EmployeeId = request.EmployeeId;
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        entity.DefaultWeeklyHours = request.DefaultWeeklyHours;
        entity.IsActive = request.IsActive;
        entity.MondayHours = request.MondayHours;
        entity.TuesdayHours = request.TuesdayHours;
        entity.WednesdayHours = request.WednesdayHours;
        entity.ThursdayHours = request.ThursdayHours;
        entity.FridayHours = request.FridayHours;
        entity.SaturdayHours = request.SaturdayHours;
        entity.SundayHours = request.SundayHours;
        entity.UpdatedBy = NormalizeNullable(request.UpdatedBy);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static EmployeeCapacityProfileDto MapToDto(EmployeeCapacityProfile entity) => new()
    {
        EmployeeCapacityProfileId = entity.EmployeeCapacityProfileId,
        EmployeeId = entity.EmployeeId,
        EffectiveFrom = entity.EffectiveFrom,
        EffectiveTo = entity.EffectiveTo,
        DefaultWeeklyHours = entity.DefaultWeeklyHours,
        IsActive = entity.IsActive,
        MondayHours = entity.MondayHours,
        TuesdayHours = entity.TuesdayHours,
        WednesdayHours = entity.WednesdayHours,
        ThursdayHours = entity.ThursdayHours,
        FridayHours = entity.FridayHours,
        SaturdayHours = entity.SaturdayHours,
        SundayHours = entity.SundayHours,
        CreatedBy = entity.CreatedBy,
        UpdatedBy = entity.UpdatedBy,
        CreatedAtUtc = entity.CreatedAtUtc,
        UpdatedAtUtc = entity.UpdatedAtUtc
    };

    private static Expression<Func<EmployeeCapacityProfile, EmployeeCapacityProfileDto>> MapToDtoExpression()
    {
        return x => new EmployeeCapacityProfileDto
        {
            EmployeeCapacityProfileId = x.EmployeeCapacityProfileId,
            EmployeeId = x.EmployeeId,
            EffectiveFrom = x.EffectiveFrom,
            EffectiveTo = x.EffectiveTo,
            DefaultWeeklyHours = x.DefaultWeeklyHours,
            IsActive = x.IsActive,
            MondayHours = x.MondayHours,
            TuesdayHours = x.TuesdayHours,
            WednesdayHours = x.WednesdayHours,
            ThursdayHours = x.ThursdayHours,
            FridayHours = x.FridayHours,
            SaturdayHours = x.SaturdayHours,
            SundayHours = x.SundayHours,
            CreatedBy = x.CreatedBy,
            UpdatedBy = x.UpdatedBy,
            CreatedAtUtc = x.CreatedAtUtc,
            UpdatedAtUtc = x.UpdatedAtUtc
        };
    }
}
