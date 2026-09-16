using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vita.Atlas.Application.Interfaces;

namespace Vita.Atlas.Api.Controllers;

[ApiController]
[Route("api/lookups")]
[Authorize]
public sealed class LookupsController : ControllerBase
{
    private readonly ILookupService _lookupService;
    private readonly IEmployeeIdentityService _employeeIdentityService;

    public LookupsController(
        ILookupService lookupService,
        IEmployeeIdentityService employeeIdentityService)
    {
        _lookupService = lookupService;
        _employeeIdentityService = employeeIdentityService;
    }

    [HttpGet("offer-statuses")]
    public async Task<IActionResult> GetOfferStatuses(CancellationToken cancellationToken) =>
        Ok(await _lookupService.GetOfferStatusesAsync(cancellationToken));

    [HttpGet("planning-partner-role-types")]
    public async Task<IActionResult> GetPlanningPartnerRoleTypes(CancellationToken cancellationToken) =>
        Ok(await _lookupService.GetPlanningPartnerRoleTypesAsync(cancellationToken));

    [HttpGet("competition-forms")]
    public async Task<IActionResult> GetCompetitionForms(CancellationToken cancellationToken) =>
        Ok(await _lookupService.GetCompetitionFormsAsync(cancellationToken));

    [HttpGet("enterprise-forms")]
    public async Task<IActionResult> GetEnterpriseForms(CancellationToken cancellationToken) =>
        Ok(await _lookupService.GetEnterpriseFormsAsync(cancellationToken));

    [HttpGet("consultant-forms")]
    public async Task<IActionResult> GetConsultantForms(CancellationToken cancellationToken) =>
        Ok(await _lookupService.GetConsultantFormsAsync(cancellationToken));

    [HttpGet("project-types")]
    public async Task<IActionResult> GetProjectTypes(CancellationToken cancellationToken) =>
        Ok(await _lookupService.GetProjectTypesAsync(cancellationToken));

    [HttpGet("project-roles")]
    public async Task<IActionResult> GetProjectRoles(CancellationToken cancellationToken) =>
        Ok(await _lookupService.GetProjectRolesAsync(cancellationToken));

    [HttpGet("complexity-levels")]
    public async Task<IActionResult> GetComplexityLevels(CancellationToken cancellationToken) =>
        Ok(await _lookupService.GetComplexityLevelsAsync(cancellationToken));

    [HttpGet("engineering-disciplines")]
    public async Task<IActionResult> GetEngineeringDisciplines(CancellationToken cancellationToken) =>
        Ok(await _lookupService.GetEngineeringDisciplinesAsync(cancellationToken));

    /// <summary>
    /// Allowed faglighed/profession values, read live from the Entra attribute definitions.
    /// Lives here with every other dropdown source rather than under its own route, so the
    /// frontend has one place to look for lookup lists regardless of where they come from.
    /// </summary>
    [HttpGet("employee-classification")]
    public async Task<IActionResult> GetEmployeeClassification(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _employeeIdentityService.GetOptionsAsync(cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }

    [HttpGet("segments")]
    public async Task<IActionResult> GetSegments(CancellationToken cancellationToken) =>
        Ok(await _lookupService.GetSegmentsAsync(cancellationToken));

    [HttpGet("virtual-resources")]
    public async Task<IActionResult> GetVirtualResources(CancellationToken cancellationToken) =>
        Ok(await _lookupService.GetVirtualResourcesAsync(cancellationToken));
}
