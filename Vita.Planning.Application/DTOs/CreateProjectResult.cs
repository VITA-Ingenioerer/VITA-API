namespace Vita.Planning.Application.DTOs;

public sealed class CreateProjectResult
{
    public int MainProjectNumber { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public List<int> SubProjectsCreated { get; set; } = [];
    public List<SubProjectFailure> SubProjectFailures { get; set; } = [];
    public bool HasSubProjectFailures => SubProjectFailures.Count > 0;
    public ProjectWorkspaceProvisioningResult? Workspace { get; set; }
}
