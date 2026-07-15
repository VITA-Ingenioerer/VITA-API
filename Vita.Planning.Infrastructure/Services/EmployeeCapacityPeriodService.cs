using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class EmployeeCapacityPeriodService : IEmployeeCapacityPeriodService
{
    private readonly PlanningDbContext _dbContext;

    public EmployeeCapacityPeriodService(PlanningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<EmployeeCapacityPeriodDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.EmployeeCapacityPeriods
            .AsNoTracking()
            .OrderBy(x => x.EmployeeId)
            .ThenBy(x => x.PeriodStart)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeCapacityPeriodDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.EmployeeCapacityPeriods
            .AsNoTracking()
            .Where(x => x.EmployeeCapacityPeriodId == id)
            .Select(MapToDtoExpression())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<EmployeeCapacityPeriodDto> CreateAsync(CreateEmployeeCapacityPeriodRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new EmployeeCapacityPeriod
        {
            EmployeeId = request.EmployeeId,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            PeriodType = request.PeriodType.Trim(),
            WeeklyHoursBasis = request.WeeklyHoursBasis,
            PublicHolidayDays = request.PublicHolidayDays,
            CapacityHours = request.CapacityHours,
            IsGenerated = request.IsGenerated,
            GenerationSource = request.GenerationSource.Trim(),
            GeneratedAtUtc = DateTime.UtcNow
        };

        _dbContext.EmployeeCapacityPeriods.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    public async Task<EmployeeCapacityPeriodDto?> UpdateAsync(int id, UpdateEmployeeCapacityPeriodRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.EmployeeCapacityPeriods
            .FirstOrDefaultAsync(x => x.EmployeeCapacityPeriodId == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        entity.EmployeeId = request.EmployeeId;
        entity.PeriodStart = request.PeriodStart;
        entity.PeriodEnd = request.PeriodEnd;
        entity.PeriodType = request.PeriodType.Trim();
        entity.WeeklyHoursBasis = request.WeeklyHoursBasis;
        entity.PublicHolidayDays = request.PublicHolidayDays;
        entity.CapacityHours = request.CapacityHours;
        entity.IsGenerated = request.IsGenerated;
        entity.GenerationSource = request.GenerationSource.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    private static EmployeeCapacityPeriodDto MapToDto(EmployeeCapacityPeriod entity) => new()
    {
        EmployeeCapacityPeriodId = entity.EmployeeCapacityPeriodId,
        EmployeeId = entity.EmployeeId,
        PeriodStart = entity.PeriodStart,
        PeriodEnd = entity.PeriodEnd,
        PeriodType = entity.PeriodType,
        WeeklyHoursBasis = entity.WeeklyHoursBasis,
        PublicHolidayDays = entity.PublicHolidayDays,
        CapacityHours = entity.CapacityHours,
        IsGenerated = entity.IsGenerated,
        GenerationSource = entity.GenerationSource,
        GeneratedAtUtc = entity.GeneratedAtUtc
    };

    private static Expression<Func<EmployeeCapacityPeriod, EmployeeCapacityPeriodDto>> MapToDtoExpression()
    {
        return x => new EmployeeCapacityPeriodDto
        {
            EmployeeCapacityPeriodId = x.EmployeeCapacityPeriodId,
            EmployeeId = x.EmployeeId,
            PeriodStart = x.PeriodStart,
            PeriodEnd = x.PeriodEnd,
            PeriodType = x.PeriodType,
            WeeklyHoursBasis = x.WeeklyHoursBasis,
            PublicHolidayDays = x.PublicHolidayDays,
            CapacityHours = x.CapacityHours,
            IsGenerated = x.IsGenerated,
            GenerationSource = x.GenerationSource,
            GeneratedAtUtc = x.GeneratedAtUtc
        };
    }
}
