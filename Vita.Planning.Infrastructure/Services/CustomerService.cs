using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class CustomerService : ICustomerService
{
    private readonly PlanningDbContext _dbContext;

    public CustomerService(PlanningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Customers
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerDto>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var normalized = query.Trim();
        return await _dbContext.Customers
            .AsNoTracking()
            .Where(x => x.Name.Contains(normalized) || (x.CvrNumber != null && x.CvrNumber.Contains(normalized)))
            .OrderBy(x => x.Name)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerDto?> GetByIdAsync(int customerId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Customers
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .Select(MapToDtoExpression())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new Customer
        {
            Name = request.Name.Trim(),
            CustomerSource = NormalizeSource(request.CustomerSource),
            CustomerStatus = "Active",
            CvrNumber = Normalize(request.CvrNumber),
            AddressLine = Normalize(request.AddressLine),
            PostalCode = Normalize(request.PostalCode),
            City = Normalize(request.City),
            CountryCode = Normalize(request.CountryCode) ?? "DK",
            CvrStatus = Normalize(request.CvrStatus),
            IndustryCode = Normalize(request.IndustryCode),
            IndustryName = Normalize(request.IndustryName),
            CreatedBy = Normalize(request.CreatedBy),
            CreatedAtUtc = DateTime.UtcNow,
        };

        _dbContext.Customers.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(entity);
    }

    public async Task<CustomerDto?> UpdateAsync(int customerId, UpdateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Customers.FirstOrDefaultAsync(x => x.CustomerId == customerId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        if (entity.CustomerSource == "Economic")
        {
            throw new InvalidOperationException("Customers synced from e-conomic cannot be edited directly.");
        }

        entity.Name = request.Name.Trim();
        entity.CustomerStatus = request.CustomerStatus;
        entity.CvrNumber = Normalize(request.CvrNumber);
        entity.AddressLine = Normalize(request.AddressLine);
        entity.PostalCode = Normalize(request.PostalCode);
        entity.City = Normalize(request.City);
        entity.CountryCode = Normalize(request.CountryCode);
        entity.CvrStatus = Normalize(request.CvrStatus);
        entity.IndustryCode = Normalize(request.IndustryCode);
        entity.IndustryName = Normalize(request.IndustryName);
        entity.UpdatedBy = Normalize(request.UpdatedBy);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(entity);
    }

    public async Task<CustomerDto?> LinkEconomicAsync(int customerId, LinkEconomicCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Customers.FirstOrDefaultAsync(x => x.CustomerId == customerId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.ExtCustomerNumber = request.ExtCustomerNumber;
        entity.CustomerSource = "Economic";
        entity.UpdatedBy = Normalize(request.UpdatedBy);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Customers.FirstOrDefaultAsync(x => x.CustomerId == customerId, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _dbContext.Customers.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CustomerDto> CreateFromCvrAsync(CreateCustomerFromCvrRequest request, IVirkService virkService, CancellationToken cancellationToken = default)
    {
        var company = await virkService.GetCompanyAsync(request.CvrNumber.Trim(), cancellationToken)
            ?? throw new KeyNotFoundException($"CVR number '{request.CvrNumber}' was not found in VIRK.");

        var existing = await _dbContext.Customers
            .FirstOrDefaultAsync(x => x.CvrNumber == company.CvrNumber, cancellationToken);

        if (existing is not null)
            return MapToDto(existing);

        var entity = new Customer
        {
            Name = company.Name,
            CustomerSource = "CVR",
            CustomerStatus = "Active",
            CvrNumber = company.CvrNumber,
            AddressLine = company.AddressLine,
            PostalCode = company.PostalCode,
            City = company.City,
            CountryCode = company.CountryCode ?? "DK",
            CvrStatus = company.Status,
            IndustryCode = company.IndustryCode,
            IndustryName = company.IndustryName,
            CreatedBy = Normalize(request.CreatedBy),
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Customers.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(entity);
    }

    private static string NormalizeSource(string? source)
    {
        return source?.Trim() switch
        {
            "Economic" => "Economic",
            "CVR" => "CVR",
            _ => "Local",
        };
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CustomerDto MapToDto(Customer entity) => new()
    {
        CustomerId = entity.CustomerId,
        ExtCustomerNumber = entity.ExtCustomerNumber,
        Name = entity.Name,
        CustomerStatus = entity.CustomerStatus,
        CustomerSource = entity.CustomerSource,
        CvrNumber = entity.CvrNumber,
        AddressLine = entity.AddressLine,
        PostalCode = entity.PostalCode,
        City = entity.City,
        CountryCode = entity.CountryCode,
        CvrStatus = entity.CvrStatus,
        IndustryCode = entity.IndustryCode,
        IndustryName = entity.IndustryName,
        SourceReference = entity.SourceReference,
        CreatedAtUtc = entity.CreatedAtUtc,
        UpdatedAtUtc = entity.UpdatedAtUtc,
        CreatedBy = entity.CreatedBy,
        UpdatedBy = entity.UpdatedBy,
    };

    private static Expression<Func<Customer, CustomerDto>> MapToDtoExpression() => x => new CustomerDto
    {
        CustomerId = x.CustomerId,
        ExtCustomerNumber = x.ExtCustomerNumber,
        Name = x.Name,
        CustomerStatus = x.CustomerStatus,
        CustomerSource = x.CustomerSource,
        CvrNumber = x.CvrNumber,
        AddressLine = x.AddressLine,
        PostalCode = x.PostalCode,
        City = x.City,
        CountryCode = x.CountryCode,
        CvrStatus = x.CvrStatus,
        IndustryCode = x.IndustryCode,
        IndustryName = x.IndustryName,
        SourceReference = x.SourceReference,
        CreatedAtUtc = x.CreatedAtUtc,
        UpdatedAtUtc = x.UpdatedAtUtc,
        CreatedBy = x.CreatedBy,
        UpdatedBy = x.UpdatedBy,
    };
}
