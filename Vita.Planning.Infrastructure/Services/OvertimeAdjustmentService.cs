using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class OvertimeAdjustmentService : IOvertimeAdjustmentService
{
    private readonly PlanningDbContext _dbContext;

    public OvertimeAdjustmentService(PlanningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<OvertimeAdjustmentDto>> GetForEmployeeAsync(
        int employeeId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.OvertimeAdjustments
            .AsNoTracking()
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.EffectiveMonth)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<OvertimeAdjustmentDto> CreateAsync(
        CreateOvertimeAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new OvertimeAdjustment
        {
            EmployeeId = request.EmployeeId,
            EffectiveMonth = request.EffectiveMonth,
            Hours = request.Hours,
            Notes = request.Notes?.Trim(),
            CreatedByEmployeeId = request.CreatedByEmployeeId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.OvertimeAdjustments.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    private static OvertimeAdjustmentDto MapToDto(OvertimeAdjustment entity) => new()
    {
        OvertimeAdjustmentId = entity.OvertimeAdjustmentId,
        EmployeeId = entity.EmployeeId,
        EffectiveMonth = entity.EffectiveMonth,
        Hours = entity.Hours,
        Notes = entity.Notes,
        CreatedByEmployeeId = entity.CreatedByEmployeeId,
        CreatedAtUtc = entity.CreatedAtUtc
    };

    private static Expression<Func<OvertimeAdjustment, OvertimeAdjustmentDto>> MapToDtoExpression()
    {
        return x => new OvertimeAdjustmentDto
        {
            OvertimeAdjustmentId = x.OvertimeAdjustmentId,
            EmployeeId = x.EmployeeId,
            EffectiveMonth = x.EffectiveMonth,
            Hours = x.Hours,
            Notes = x.Notes,
            CreatedByEmployeeId = x.CreatedByEmployeeId,
            CreatedAtUtc = x.CreatedAtUtc
        };
    }
}
