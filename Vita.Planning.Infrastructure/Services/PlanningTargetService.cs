using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class PlanningTargetService : IPlanningTargetService
{
    private readonly PlanningDbContext _dbContext;

    public PlanningTargetService(PlanningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PlanningTargetDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.PlanningTargets
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<PlanningTargetDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PlanningTargets
            .AsNoTracking()
            .Where(x => x.PlanningTargetId == id)
            .Select(MapToDtoExpression())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PlanningTargetDto> CreateAsync(CreatePlanningTargetRequest request, CancellationToken cancellationToken = default)
    {
        ValidateReferenceCombination(
            request.ExtProjectNumber,
            request.OfferId,
            request.InternalPlanningCodeId);

        if (request.InternalPlanningCodeId.HasValue)
        {
            var codeExists = await _dbContext.InternalPlanningCodes
                .AnyAsync(x => x.InternalPlanningCodeId == request.InternalPlanningCodeId.Value && x.IsActive, cancellationToken);
            if (!codeExists)
                throw new InvalidOperationException($"InternalPlanningCode with id {request.InternalPlanningCodeId.Value} does not exist.");
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();

        var exists = await _dbContext.PlanningTargets
            .AnyAsync(x => x.Code == normalizedCode, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"Planning target '{normalizedCode}' already exists.");
        }

        var entity = new PlanningTarget
        {
            Code = normalizedCode,
            Name = request.Name.Trim(),
            TargetType = request.TargetType.Trim(),
            ExtProjectNumber = request.ExtProjectNumber,
            OfferId = request.OfferId,
            InternalPlanningCodeId = request.InternalPlanningCodeId,
            OfficeCode = NormalizeNullableUpper(request.OfficeCode),
            IsActive = request.IsActive,
            IsPlannable = request.IsPlannable,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.PlanningTargets.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    public async Task<PlanningTargetDto?> UpdateAsync(int id, UpdatePlanningTargetRequest request, CancellationToken cancellationToken = default)
    {
        ValidateReferenceCombination(
            request.ExtProjectNumber,
            request.OfferId,
            request.InternalPlanningCodeId);

        if (request.InternalPlanningCodeId.HasValue)
        {
            var exists = await _dbContext.InternalPlanningCodes
                .AnyAsync(x => x.InternalPlanningCodeId == request.InternalPlanningCodeId.Value && x.IsActive, cancellationToken);
            if (!exists)
                throw new InvalidOperationException($"InternalPlanningCode with id {request.InternalPlanningCodeId.Value} does not exist.");
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();

        var entity = await _dbContext.PlanningTargets
            .FirstOrDefaultAsync(x => x.PlanningTargetId == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var duplicateExists = await _dbContext.PlanningTargets
            .AnyAsync(x => x.PlanningTargetId != id && x.Code == normalizedCode, cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException($"Planning target '{normalizedCode}' already exists.");
        }

        entity.Code = normalizedCode;
        entity.Name = request.Name.Trim();
        entity.TargetType = request.TargetType.Trim();
        entity.ExtProjectNumber = request.ExtProjectNumber;
        entity.OfferId = request.OfferId;
        entity.InternalPlanningCodeId = request.InternalPlanningCodeId;
        entity.OfficeCode = NormalizeNullableUpper(request.OfficeCode);
        entity.IsActive = request.IsActive;
        entity.IsPlannable = request.IsPlannable;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    private static void ValidateReferenceCombination(
        int? extProjectNumber,
        int? offerId,
        int? internalPlanningCodeId)
    {
        var populatedCount = 0;

        if (extProjectNumber.HasValue) populatedCount++;
        if (offerId.HasValue) populatedCount++;
        if (internalPlanningCodeId.HasValue) populatedCount++;

        if (populatedCount > 1)
        {
            throw new InvalidOperationException(
                "Only one of ExtProjectNumber, OfferId, or InternalPlanningCodeId can be set.");
        }
    }

    private static string? NormalizeNullableUpper(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    }

    private static PlanningTargetDto MapToDto(PlanningTarget entity)
    {
        return new PlanningTargetDto
        {
            PlanningTargetId = entity.PlanningTargetId,
            Code = entity.Code,
            Name = entity.Name,
            TargetType = entity.TargetType,
            ExtProjectNumber = entity.ExtProjectNumber,
            OfferId = entity.OfferId,
            InternalPlanningCodeId = entity.InternalPlanningCodeId,
            OfficeCode = entity.OfficeCode,
            IsActive = entity.IsActive,
            IsPlannable = entity.IsPlannable,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    private static Expression<Func<PlanningTarget, PlanningTargetDto>> MapToDtoExpression()
    {
        return entity => new PlanningTargetDto
        {
            PlanningTargetId = entity.PlanningTargetId,
            Code = entity.Code,
            Name = entity.Name,
            TargetType = entity.TargetType,
            ExtProjectNumber = entity.ExtProjectNumber,
            OfferId = entity.OfferId,
            InternalPlanningCodeId = entity.InternalPlanningCodeId,
            OfficeCode = entity.OfficeCode,
            IsActive = entity.IsActive,
            IsPlannable = entity.IsPlannable,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }
}