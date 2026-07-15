namespace Vita.Planning.Application.DTOs;

public sealed class ConvertOfferToProjectResult
{
    public int OfferId { get; set; }
    public string OfferNumber { get; set; } = string.Empty;
    public int MainProjectNumber { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public DateTime ConvertedAtUtc { get; set; }
    public List<SubProjectCreatedResult> SubProjectsCreated { get; set; } = [];
    public List<SubProjectFailure> SubProjectFailures { get; set; } = [];
    public bool HasSubProjectFailures => SubProjectFailures.Count > 0;
    public int? ResourcePlanEntriesMigrated { get; set; }
    public ProjectWorkspaceProvisioningResult? Workspace { get; set; }
}

public sealed class SubProjectCreatedResult
{
    public int SubProjectNumber { get; set; }
    public string SubProjectName { get; set; } = string.Empty;
}

public sealed class SubProjectFailure
{
    public int SubProjectNumber { get; set; }
    public string SubProjectName { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public bool WasAttempted { get; set; }
}
