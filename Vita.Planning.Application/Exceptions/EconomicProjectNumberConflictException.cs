namespace Vita.Planning.Application.Exceptions;

public sealed class EconomicProjectNumberConflictException : InvalidOperationException
{
    public int ProjectNumber { get; }

    public EconomicProjectNumberConflictException(int projectNumber)
        : base($"e-conomic reports project number {projectNumber} already exists.")
    {
        ProjectNumber = projectNumber;
    }
}
