using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerService _service;
    private readonly IBusinessEventService _events;
    private readonly ICompanyContactService _contacts;

    public CustomersController(ICustomerService service, IBusinessEventService events, ICompanyContactService contacts)
    {
        _service = service;
        _events = events;
        _contacts = contacts;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("query is required.");
        }

        var result = await _service.SearchAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{customerId:int}")]
    public async Task<IActionResult> GetById(int customerId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(customerId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var caller = CallerInfo.FromClaimsPrincipal(User);
        var result = await _service.CreateAsync(request, cancellationToken);

        await _events.RecordAsync(new RecordBusinessEventRequest
        {
            EventType = "CustomerCreated",
            EventTitle = $"Kunde oprettet: {result.Name}",
            EntityType = "Customer",
            EntityId = result.CustomerId.ToString(),
            NewValue = result.Name,
            CreatedByUserId = caller.UserId,
            CreatedByName = caller.Name,
            SourceModule = "CustomersController"
        }, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { customerId = result.CustomerId }, result);
    }

    [HttpPut("{customerId:int}")]
    public async Task<IActionResult> Update(int customerId, [FromBody] UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var caller = CallerInfo.FromClaimsPrincipal(User);
            var result = await _service.UpdateAsync(customerId, request, cancellationToken);

            if (result is null)
            {
                return NotFound();
            }

            await _events.RecordAsync(new RecordBusinessEventRequest
            {
                EventType = "CustomerUpdated",
                EventTitle = $"Kunde opdateret: {result.Name}",
                EntityType = "Customer",
                EntityId = result.CustomerId.ToString(),
                NewValue = result.Name,
                CreatedByUserId = caller.UserId,
                CreatedByName = caller.Name,
                SourceModule = "CustomersController"
            }, cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{customerId:int}")]
    public async Task<IActionResult> Delete(int customerId, CancellationToken cancellationToken)
    {
        var deleted = await _service.DeleteAsync(customerId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("{customerId:int}/contacts")]
    public async Task<IActionResult> GetContacts(int customerId, CancellationToken cancellationToken)
    {
        var result = await _contacts.GetByCustomerIdAsync(customerId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{customerId:int}/contacts")]
    public async Task<IActionResult> CreateContact(
        int customerId,
        [FromBody] CreateCompanyContactRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _contacts.CreateAsync(customerId, request, cancellationToken);
            return CreatedAtAction(nameof(GetContacts), new { customerId }, result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("{customerId:int}/link-economic")]
    public async Task<IActionResult> LinkEconomic(int customerId, [FromBody] LinkEconomicCustomerRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.LinkEconomicAsync(customerId, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
