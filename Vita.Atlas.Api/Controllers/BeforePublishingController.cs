using Microsoft.AspNetCore.Mvc;
using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Api.Controllers;

/// <summary>
/// The ordered "run this before publishing" pass: every step needed to bring the database in
/// line with its real sources, numbered so Swagger lists them in the order they must run.
///
/// Every step here is a façade. It resolves the controller that already owns the work and calls
/// its action, so there is exactly one implementation of each import or sync and this group can
/// never drift from the endpoints it fronts. Those endpoints stay where they are and keep
/// working on their own — this only gives them a running order.
///
/// The order is not cosmetic:
///   1 must precede 4, because project metadata rows for projects e-conomic has not sent yet are
///     skipped rather than imported;
///   1 must precede 5, because planned hours for an unknown project are filed as an internal
///     planning code instead of against the real project;
///   3 must precede 6, because the overtime balance is computed from time entries.
/// </summary>
[ApiController]
[Route("api/before-publishing")]
[Tags("Before publishing")]
public sealed class BeforePublishingController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;

    public BeforePublishingController(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// The running order, as data. Answers "what do I run, in what order, and what does each one
    /// need" without anyone having to read this file.
    /// </summary>
    [HttpGet("00-checklist")]
    public ActionResult<BeforePublishingChecklist> GetChecklist() => Ok(new BeforePublishingChecklist
    {
        Steps =
        [
            new()
            {
                Step = 1,
                Endpoint = "POST /api/before-publishing/01-economic-master-data",
                Source = "e-conomic",
                Description = "Statuses, groups, customers, employee groups, employees, users, activities, projects, project activities — in dependency order.",
                AlsoAvailableAt = "POST /api/sync/run-all"
            },
            new()
            {
                Step = 2,
                Endpoint = "POST /api/before-publishing/02-employee-classification",
                Source = "Microsoft Entra",
                Description = "Pulls faglighed/profession for the whole roster into the local read model.",
                AlsoAvailableAt = "POST /api/sync/employee-classification"
            },
            new()
            {
                Step = 3,
                Endpoint = "POST /api/before-publishing/03-time-entries",
                Source = "e-conomic",
                Description = "Full reload of registered time. Not part of run-all, which is why it is its own step.",
                AlsoAvailableAt = "POST /api/sync/time-entries/all"
            },
            new()
            {
                Step = 4,
                Endpoint = "POST /api/before-publishing/04-project-metadata",
                Source = "Ressourceplan - Back end.xlsm (upload, sheet 'Projekter')",
                Description = "Project metadata, offer metadata and internal planning codes. Takes a file. Run with dryRun=true first.",
                RequiresFileUpload = true,
                AlsoAvailableAt = "POST /api/import/ressourceplan-workbook-metadata"
            },
            new()
            {
                Step = 5,
                Endpoint = "POST /api/before-publishing/05-planned-hours",
                Source = "Ressourceplan workbook in SharePoint (sheet 'Timer-tabel')",
                Description = "Planned hours into resource_plan_entries. Reads the SharePoint copy, not an upload — check it is the current one.",
                AlsoAvailableAt = "POST /api/import/legacy-ressourceplan"
            },
            new()
            {
                Step = 6,
                Endpoint = "POST /api/before-publishing/06-overtime-balance",
                Source = "derived from time entries",
                Description = "Full recompute. Run last; it depends on step 3.",
                AlsoAvailableAt = "POST /api/sync/overtime-balance/refresh-all"
            }
        ]
    });

    /// <summary>Step 1 — e-conomic master data, in dependency order.</summary>
    [HttpPost("01-economic-master-data")]
    public Task<IActionResult> Step1EconomicMasterData(CancellationToken cancellationToken) =>
        Delegate<SyncController, IActionResult>(c => c.RunAll(cancellationToken));

    /// <summary>Step 2 — employee classification from Entra.</summary>
    [HttpPost("02-employee-classification")]
    public Task<IActionResult> Step2EmployeeClassification(CancellationToken cancellationToken) =>
        Delegate<SyncController, IActionResult>(c => c.SyncEmployeeClassification(cancellationToken));

    /// <summary>Step 3 — full time-entry reload.</summary>
    [HttpPost("03-time-entries")]
    public Task<IActionResult> Step3TimeEntries(CancellationToken cancellationToken) =>
        Delegate<SyncController, IActionResult>(c => c.SyncAllTimeEntries(cancellationToken));

    /// <summary>
    /// Step 4 — project and offer metadata from the uploaded workbook.
    /// Leave <paramref name="target"/> empty: the default routing is what sends numeric rows to
    /// project metadata, T-numbers to offers and everything else to internal planning codes.
    /// </summary>
    [HttpPost("04-project-metadata")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public Task<ActionResult<PlanningMetadataImportResult>> Step4ProjectMetadata(
        IFormFile file,
        [FromQuery] string? target,
        [FromQuery] bool dryRun,
        CancellationToken cancellationToken) =>
        Delegate<PlanningMetadataImportController, ActionResult<PlanningMetadataImportResult>>(
            c => c.ImportRessourceplanWorkbookMetadata(file, target, dryRun, cancellationToken));

    /// <summary>Step 5 — planned hours from the Ressourceplan workbook. ScenarioId 0 = default scenario.</summary>
    [HttpPost("05-planned-hours")]
    public Task<ActionResult<LegacyImportResult>> Step5PlannedHours(
        [FromBody] LegacyImportRequest request,
        CancellationToken cancellationToken) =>
        Delegate<LegacyImportController, ActionResult<LegacyImportResult>>(
            c => c.Import(request, cancellationToken));

    /// <summary>Step 6 — recompute every employee's overtime balance.</summary>
    [HttpPost("06-overtime-balance")]
    public Task<IActionResult> Step6OvertimeBalance(CancellationToken cancellationToken) =>
        Delegate<SyncController, IActionResult>(c => c.RefreshOvertimeBalanceAll(cancellationToken));

    /// <summary>
    /// Builds the owning controller through DI and runs its action.
    ///
    /// Delegating rather than re-implementing is the whole point of this group: the sequence in
    /// run-all is already duplicated once in ScheduledSyncHostedService and the two have drifted
    /// (one syncs project activities, the other time entries). A third copy would be a third
    /// thing to keep in step.
    ///
    /// ControllerContext is carried across so the delegate sees the same HttpContext and the
    /// same User — several of these resolve the caller from claims to stamp who ran the import.
    /// </summary>
    private async Task<TResult> Delegate<TController, TResult>(Func<TController, Task<TResult>> action)
        where TController : ControllerBase
    {
        var controller = ActivatorUtilities.CreateInstance<TController>(_serviceProvider);

        try
        {
            controller.ControllerContext = ControllerContext;
            return await action(controller);
        }
        finally
        {
            // Created outside DI's disposal tracking, so anything disposable it holds is ours to
            // release. The scoped services it was built from are not disposed by this.
            if (controller is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}

public sealed class BeforePublishingChecklist
{
    public string Message { get; set; } =
        "Run the steps in order. Each one also exists on its own endpoint — see AlsoAvailableAt.";

    public List<BeforePublishingStep> Steps { get; set; } = [];
}

public sealed class BeforePublishingStep
{
    public int Step { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RequiresFileUpload { get; set; }

    /// <summary>The endpoint that owns the work. This group only calls it.</summary>
    public string AlsoAvailableAt { get; set; } = string.Empty;
}
