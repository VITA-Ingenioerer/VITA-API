using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class PublicHolidayCalendarService : IPublicHolidayCalendarService
{
    private readonly PlanningDbContext _dbContext;

    public PublicHolidayCalendarService(PlanningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PublicHolidayCalendarDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.PublicHolidayCalendars
            .AsNoTracking()
            .OrderBy(x => x.CountryCode)
            .ThenBy(x => x.HolidayDate)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<PublicHolidayCalendarDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PublicHolidayCalendars
            .AsNoTracking()
            .Where(x => x.PublicHolidayCalendarId == id)
            .Select(MapToDtoExpression())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PublicHolidayCalendarDto> CreateAsync(CreatePublicHolidayCalendarRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new PublicHolidayCalendar
        {
            CountryCode = request.CountryCode.Trim().ToUpperInvariant(),
            HolidayDate = request.HolidayDate,
            HolidayName = request.HolidayName.Trim(),
            IsPublicHoliday = request.IsPublicHoliday,
            IsHalfDay = request.IsHalfDay,
            HoursReduction = request.HoursReduction,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.PublicHolidayCalendars.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    public async Task<object> SyncAsync(
        string countryCode,
        int year,
        IReadOnlyList<PublicHolidayCalendarDto> holidays,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            throw new InvalidOperationException("CountryCode is required.");
        }

        if (year < 2000 || year > 2100)
        {
            throw new InvalidOperationException("Year must be between 2000 and 2100.");
        }

        var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();

        var holidayDates = holidays
            .Select(x => x.HolidayDate)
            .Distinct()
            .ToList();

        var existing = await _dbContext.PublicHolidayCalendars
            .Where(x => x.CountryCode == normalizedCountryCode && holidayDates.Contains(x.HolidayDate))
            .ToDictionaryAsync(x => x.HolidayDate, cancellationToken);

        var inserted = 0;
        var updated = 0;

        foreach (var holiday in holidays)
        {
            var holidayDate = holiday.HolidayDate;

            if (existing.TryGetValue(holidayDate, out var entity))
            {
                var holidayName = holiday.HolidayName?.Trim();
                var isChanged = entity.HolidayName != holidayName
                    || entity.IsPublicHoliday != true
                    || entity.IsHalfDay
                    || entity.HoursReduction != null;

                if (isChanged)
                {
                    entity.HolidayName = holidayName ?? entity.HolidayName;
                    entity.IsPublicHoliday = true;
                    entity.IsHalfDay = false;
                    entity.HoursReduction = null;
                    updated++;
                }

                continue;
            }

            _dbContext.PublicHolidayCalendars.Add(new PublicHolidayCalendar
            {
                CountryCode = normalizedCountryCode,
                HolidayDate = holidayDate,
                HolidayName = string.IsNullOrWhiteSpace(holiday.HolidayName) ? normalizedCountryCode : holiday.HolidayName.Trim(),
                IsPublicHoliday = true,
                IsHalfDay = false,
                HoursReduction = null,
                CreatedAtUtc = DateTime.UtcNow
            });

            inserted++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new
        {
            CountryCode = normalizedCountryCode,
            Year = year,
            RowsRead = holidays.Count,
            RowsInserted = inserted,
            RowsUpdated = updated,
            Message = "Public holidays synced. Vita holidays automatically reflect the changes through the merged view."
        };
    }

    public async Task<PublicHolidayCalendarDto?> UpdateAsync(int id, UpdatePublicHolidayCalendarRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.PublicHolidayCalendars
            .FirstOrDefaultAsync(x => x.PublicHolidayCalendarId == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        entity.CountryCode = request.CountryCode.Trim().ToUpperInvariant();
        entity.HolidayDate = request.HolidayDate;
        entity.HolidayName = request.HolidayName.Trim();
        entity.IsPublicHoliday = request.IsPublicHoliday;
        entity.IsHalfDay = request.IsHalfDay;
        entity.HoursReduction = request.HoursReduction;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    private static PublicHolidayCalendarDto MapToDto(PublicHolidayCalendar entity) => new()
    {
        PublicHolidayCalendarId = entity.PublicHolidayCalendarId,
        CountryCode = entity.CountryCode,
        HolidayDate = entity.HolidayDate,
        HolidayName = entity.HolidayName,
        IsPublicHoliday = entity.IsPublicHoliday,
        IsHalfDay = entity.IsHalfDay,
        HoursReduction = entity.HoursReduction,
        CreatedAtUtc = entity.CreatedAtUtc
    };

    private static Expression<Func<PublicHolidayCalendar, PublicHolidayCalendarDto>> MapToDtoExpression()
    {
        return x => new PublicHolidayCalendarDto
        {
            PublicHolidayCalendarId = x.PublicHolidayCalendarId,
            CountryCode = x.CountryCode,
            HolidayDate = x.HolidayDate,
            HolidayName = x.HolidayName,
            IsPublicHoliday = x.IsPublicHoliday,
            IsHalfDay = x.IsHalfDay,
            HoursReduction = x.HoursReduction,
            CreatedAtUtc = x.CreatedAtUtc
        };
    }

}
