using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class EmployeeCapacityOverrideService : IEmployeeCapacityOverrideService
{
    private readonly PlanningDbContext _dbContext;

    public EmployeeCapacityOverrideService(PlanningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<EmployeeCapacityOverrideDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.EmployeeCapacityOverrides
            .AsNoTracking()
            .OrderBy(x => x.EmployeeId)
            .ThenBy(x => x.EffectiveFrom)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeCapacityOverrideDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.EmployeeCapacityOverrides
            .AsNoTracking()
            .Where(x => x.EmployeeCapacityOverrideId == id)
            .Select(MapToDtoExpression())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<EmployeeCapacityOverrideDto> CreateAsync(CreateEmployeeCapacityOverrideRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new EmployeeCapacityOverride
        {
            EmployeeId = request.EmployeeId,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            OverrideType = request.OverrideType.Trim(),
            WeeklyHours = request.WeeklyHours,
            CapacityFactor = request.CapacityFactor,
            FixedHoursPerWeek = request.FixedHoursPerWeek,
            Reason = NormalizeNullable(request.Reason),
            Notes = NormalizeNullable(request.Notes),
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

        _dbContext.EmployeeCapacityOverrides.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    public async Task<EmployeeCapacityOverrideDto?> UpdateAsync(int id, UpdateEmployeeCapacityOverrideRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.EmployeeCapacityOverrides
            .FirstOrDefaultAsync(x => x.EmployeeCapacityOverrideId == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        entity.EmployeeId = request.EmployeeId;
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        entity.OverrideType = request.OverrideType.Trim();
        entity.WeeklyHours = request.WeeklyHours;
        entity.CapacityFactor = request.CapacityFactor;
        entity.FixedHoursPerWeek = request.FixedHoursPerWeek;
        entity.Reason = NormalizeNullable(request.Reason);
        entity.Notes = NormalizeNullable(request.Notes);
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

    private static EmployeeCapacityOverrideDto MapToDto(EmployeeCapacityOverride entity) => new()
    {
        EmployeeCapacityOverrideId = entity.EmployeeCapacityOverrideId,
        EmployeeId = entity.EmployeeId,
        EffectiveFrom = entity.EffectiveFrom,
        EffectiveTo = entity.EffectiveTo,
        OverrideType = entity.OverrideType,
        WeeklyHours = entity.WeeklyHours,
        CapacityFactor = entity.CapacityFactor,
        FixedHoursPerWeek = entity.FixedHoursPerWeek,
        Reason = entity.Reason,
        Notes = entity.Notes,
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

    private static Expression<Func<EmployeeCapacityOverride, EmployeeCapacityOverrideDto>> MapToDtoExpression()
    {
        return x => new EmployeeCapacityOverrideDto
        {
            EmployeeCapacityOverrideId = x.EmployeeCapacityOverrideId,
            EmployeeId = x.EmployeeId,
            EffectiveFrom = x.EffectiveFrom,
            EffectiveTo = x.EffectiveTo,
            OverrideType = x.OverrideType,
            WeeklyHours = x.WeeklyHours,
            CapacityFactor = x.CapacityFactor,
            FixedHoursPerWeek = x.FixedHoursPerWeek,
            Reason = x.Reason,
            Notes = x.Notes,
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
