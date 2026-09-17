using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vita.Atlas.Application.DTOs;
using Vita.Atlas.Infrastructure.Data;
using Vita.Atlas.Infrastructure.Data.Entities;

namespace Vita.Atlas.Api.Controllers;

/// <summary>
/// Manages the plannable non-employees: unfilled roles (NN-BIM) and named people at partner
/// companies (Lars J at PLH arkitekter). Both live in core.virtual_resources and are told apart
/// by whether a customer is linked.
///
/// Admin-only by deliberate choice: the code is what the Timer-tabel import matches initials
/// against, so a list that accumulates NN-BIM alongside NN-Bim stops being a lookup and starts
/// being a source of duplicate rows in the planner.
///
/// Reads stay open to any planner user — the grid has to render these rows for everyone.
/// </summary>
[ApiController]
[Route("api/virtual-resources")]
public sealed class VirtualResourcesController : ControllerBase
{
    private readonly AtlasDbContext _dbContext;

    public VirtualResourcesController(AtlasDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Every virtual resource. Unlike GET /api/lookups/virtual-resources, which serves only the
    /// active ones for the planner, this includes retired rows so an admin can see and reactivate
    /// them.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VirtualResourceDto>>> GetAll(
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.VirtualResources.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return Ok(await query
            .OrderBy(x => x.CustomerId == null ? 0 : 1)
            .ThenBy(x => x.Name)
            .Select(x => new VirtualResourceDto
            {
                VirtualResourceId = x.VirtualResourceId,
                Code = x.Code,
                Name = x.Name,
                DisciplineId = x.DisciplineId,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer == null ? null : x.Customer.Name,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "AdminAccess")]
    public async Task<ActionResult<VirtualResourceDto>> Create(
        [FromBody] UpsertVirtualResourceRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(request, existingId: null, cancellationToken);

        if (validation is not null)
        {
            return BadRequest(new { message = validation });
        }

        var entity = new VirtualResource
        {
            Code = NormalizeCode(request.Code),
            Name = request.Name.Trim(),
            DisciplineId = request.DisciplineId,
            CustomerId = request.CustomerId,
            IsActive = request.IsActive
        };

        _dbContext.VirtualResources.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetAll), new { }, await ToDtoAsync(entity, cancellationToken));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "AdminAccess")]
    public async Task<ActionResult<VirtualResourceDto>> Update(
        int id,
        [FromBody] UpsertVirtualResourceRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.VirtualResources
            .FirstOrDefaultAsync(x => x.VirtualResourceId == id, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        var validation = await ValidateAsync(request, existingId: id, cancellationToken);

        if (validation is not null)
        {
            return BadRequest(new { message = validation });
        }

        entity.Code = NormalizeCode(request.Code);
        entity.Name = request.Name.Trim();
        entity.DisciplineId = request.DisciplineId;
        entity.CustomerId = request.CustomerId;
        entity.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(await ToDtoAsync(entity, cancellationToken));
    }

    /// <summary>
    /// Retires a virtual resource. Deactivation, not deletion: its resource plan and every hour
    /// planned against it stay, and a hard delete would orphan them.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "AdminAccess")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.VirtualResources
            .FirstOrDefaultAsync(x => x.VirtualResourceId == id, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        entity.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<string?> ValidateAsync(
        UpsertVirtualResourceRequest request,
        int? existingId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return "Code is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Name is required.";
        }

        var code = NormalizeCode(request.Code);

        if (code.Length > 50)
        {
            return "Code must be 50 characters or fewer.";
        }

        if (request.Name.Trim().Length > 200)
        {
            return "Name must be 200 characters or fewer.";
        }

        // Checked here as well as by the unique index so the caller gets a sentence naming the
        // clash rather than a constraint violation.
        var codeTaken = await _dbContext.VirtualResources
            .AnyAsync(x => x.Code == code && (existingId == null || x.VirtualResourceId != existingId), cancellationToken);

        if (codeTaken)
        {
            return $"The code '{code}' is already used by another virtual resource.";
        }

        if (request.CustomerId.HasValue)
        {
            var customerExists = await _dbContext.Customers
                .AnyAsync(x => x.CustomerId == request.CustomerId.Value, cancellationToken);

            if (!customerExists)
            {
                return $"Customer {request.CustomerId.Value} was not found.";
            }
        }

        return null;
    }

    // Upper-cased because the Timer-tabel import matches it against initials case-insensitively;
    // storing one canonical form keeps the unique index from admitting NN-BIM and nn-bim both.
    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private async Task<VirtualResourceDto> ToDtoAsync(VirtualResource entity, CancellationToken cancellationToken)
    {
        var customerName = entity.CustomerId.HasValue
            ? await _dbContext.Customers
                .Where(x => x.CustomerId == entity.CustomerId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return new VirtualResourceDto
        {
            VirtualResourceId = entity.VirtualResourceId,
            Code = entity.Code,
            Name = entity.Name,
            DisciplineId = entity.DisciplineId,
            CustomerId = entity.CustomerId,
            CustomerName = customerName,
            IsActive = entity.IsActive
        };
    }
}
