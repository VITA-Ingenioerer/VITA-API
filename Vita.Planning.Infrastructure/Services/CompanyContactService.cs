using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class CompanyContactService : ICompanyContactService
{
    private readonly PlanningDbContext _dbContext;

    public CompanyContactService(PlanningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CompanyContactDto>> GetByCustomerIdAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CompanyContacts
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => MapToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<CompanyContactDto> CreateAsync(
        int customerId,
        CreateCompanyContactRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerExists = await _dbContext.Customers
            .AnyAsync(x => x.CustomerId == customerId, cancellationToken);

        if (!customerExists)
            throw new InvalidOperationException($"Customer with id {customerId} does not exist.");

        var entity = new CompanyContact
        {
            CustomerId = customerId,
            Name = request.Name.Trim(),
            Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.CompanyContacts.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    private static CompanyContactDto MapToDto(CompanyContact x) => new()
    {
        CompanyContactId = x.CompanyContactId,
        CustomerId = x.CustomerId,
        Name = x.Name,
        Title = x.Title,
        Email = x.Email,
        Phone = x.Phone,
        IsActive = x.IsActive,
        CreatedAtUtc = x.CreatedAtUtc,
        UpdatedAtUtc = x.UpdatedAtUtc
    };
}
