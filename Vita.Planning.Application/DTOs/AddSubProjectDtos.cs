namespace Vita.Planning.Application.DTOs;

public sealed class AddSubProjectRequest
{
    /// <summary>
    /// Only the distinguishing part of the name. The main project's own name is prefixed
    /// with " - " server-side, so the caller cannot drift from the convention that
    /// CreateProjectAsync already established for sub-projects created up front.
    /// </summary>
    public string NameSuffix { get; set; } = string.Empty;
}

public sealed class AddSubProjectResult
{
    public int SubProjectNumber { get; set; }

    public string SubProjectName { get; set; } = string.Empty;

    public int MainProjectNumber { get; set; }
}
