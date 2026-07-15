using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class VitaHolidayService : IVitaHolidayService
{
    private static readonly HashSet<string> AllowedHolidayTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Company",
        "PublicOverride"
    };

    private readonly PlanningDbContext _dbContext;

    public VitaHolidayService(PlanningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<VitaHolidayDto>> GetAllAsync(
        string? countryCode = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Set<VitaHoliday>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.CountryCode == normalizedCountryCode);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(x => x.HolidayDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => x.HolidayDate <= toDate.Value);
        }

        return await query
            .OrderBy(x => x.CountryCode)
            .ThenBy(x => x.HolidayDate)
            .Select(x => new VitaHolidayDto
            {
                VitaHolidayOverrideId = x.VitaHolidayOverrideId,
                PublicHolidayCalendarId = x.PublicHolidayCalendarId,
                CountryCode = x.CountryCode,
                HolidayDate = x.HolidayDate,
                HolidayName = x.HolidayName,
                IsActive = x.IsActive,
                IsHalfDay = x.IsHalfDay,
                HoursReduction = x.HoursReduction,
                HolidayType = x.HolidayType,
                Notes = x.Notes,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VitaHolidayOverrideDto>> GetOverridesAsync(
        string? countryCode = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Set<VitaHolidayOverride>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.CountryCode == normalizedCountryCode);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(x => x.HolidayDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => x.HolidayDate <= toDate.Value);
        }

        return await query
            .OrderBy(x => x.CountryCode)
            .ThenBy(x => x.HolidayDate)
            .Select(MapOverrideToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<VitaHolidayOverrideDto?> GetOverrideByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<VitaHolidayOverride>()
            .AsNoTracking()
            .Where(x => x.VitaHolidayOverrideId == id)
            .Select(MapOverrideToDto())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<VitaHolidayOverrideDto> CreateOverrideAsync(CreateVitaHolidayOverrideRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request.CountryCode, request.HolidayDate, request.HolidayType, request.HoursReduction);

        var normalizedCountryCode = request.CountryCode.Trim().ToUpperInvariant();
        var normalizedHolidayType = NormalizeHolidayType(request.HolidayType);

        var duplicateExists = await _dbContext.Set<VitaHolidayOverride>()
            .AnyAsync(x => x.CountryCode == normalizedCountryCode && x.HolidayDate == request.HolidayDate, cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("A holiday override already exists for this country and date.");
        }

        var entity = new VitaHolidayOverride
        {
            CountryCode = normalizedCountryCode,
            HolidayDate = request.HolidayDate,
            HolidayName = NormalizeNullable(request.HolidayName),
            IsActive = request.IsActive,
            IsHalfDay = request.IsHalfDay,
            HoursReduction = request.HoursReduction,
            HolidayType = normalizedHolidayType,
            Notes = NormalizeNullable(request.Notes),
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Set<VitaHolidayOverride>().Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapOverrideToDto(entity);
    }

    public async Task<VitaHolidayOverrideDto?> UpdateOverrideAsync(int id, UpdateVitaHolidayOverrideRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request.CountryCode, request.HolidayDate, request.HolidayType, request.HoursReduction);

        var entity = await _dbContext.Set<VitaHolidayOverride>()
            .FirstOrDefaultAsync(x => x.VitaHolidayOverrideId == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var normalizedCountryCode = request.CountryCode.Trim().ToUpperInvariant();
        var duplicateExists = await _dbContext.Set<VitaHolidayOverride>()
            .AnyAsync(x => x.VitaHolidayOverrideId != id && x.CountryCode == normalizedCountryCode && x.HolidayDate == request.HolidayDate, cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("A holiday override already exists for this country and date.");
        }

        entity.CountryCode = normalizedCountryCode;
        entity.HolidayDate = request.HolidayDate;
        entity.HolidayName = NormalizeNullable(request.HolidayName);
        entity.IsActive = request.IsActive;
        entity.IsHalfDay = request.IsHalfDay;
        entity.HoursReduction = request.HoursReduction;
        entity.HolidayType = NormalizeHolidayType(request.HolidayType);
        entity.Notes = NormalizeNullable(request.Notes);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapOverrideToDto(entity);
    }

    private static void ValidateRequest(string countryCode, DateOnly holidayDate, string holidayType, decimal? hoursReduction)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            throw new InvalidOperationException("CountryCode is required.");
        }

        if (holidayDate == default)
        {
            throw new InvalidOperationException("HolidayDate is required.");
        }

        if (string.IsNullOrWhiteSpace(holidayType))
        {
            throw new InvalidOperationException("HolidayType is required.");
        }

        if (!AllowedHolidayTypes.Contains(holidayType.Trim()))
        {
            throw new InvalidOperationException("HolidayType must be either 'Company' or 'PublicOverride'.");
        }

        if (hoursReduction.HasValue && hoursReduction.Value < 0)
        {
            throw new InvalidOperationException("HoursReduction cannot be negative.");
        }
    }

    private static string NormalizeHolidayType(string holidayType)
    {
        var trimmed = holidayType.Trim();
        return trimmed.Equals("PublicOverride", StringComparison.OrdinalIgnoreCase)
            ? "PublicOverride"
            : "Company";
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static VitaHolidayOverrideDto MapOverrideToDto(VitaHolidayOverride entity)
    {
        return new VitaHolidayOverrideDto
        {
            VitaHolidayOverrideId = entity.VitaHolidayOverrideId,
            CountryCode = entity.CountryCode,
            HolidayDate = entity.HolidayDate,
            HolidayName = entity.HolidayName,
            IsActive = entity.IsActive,
            IsHalfDay = entity.IsHalfDay,
            HoursReduction = entity.HoursReduction,
            HolidayType = entity.HolidayType,
            Notes = entity.Notes,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    private static System.Linq.Expressions.Expression<Func<VitaHolidayOverride, VitaHolidayOverrideDto>> MapOverrideToDto()
    {
        return entity => new VitaHolidayOverrideDto
        {
            VitaHolidayOverrideId = entity.VitaHolidayOverrideId,
            CountryCode = entity.CountryCode,
            HolidayDate = entity.HolidayDate,
            HolidayName = entity.HolidayName,
            IsActive = entity.IsActive,
            IsHalfDay = entity.IsHalfDay,
            HoursReduction = entity.HoursReduction,
            HolidayType = entity.HolidayType,
            Notes = entity.Notes,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }
}
