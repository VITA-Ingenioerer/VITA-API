namespace Vita.Planning.Application.Interfaces;

/// <summary>
/// Resolves a single Guid for the lifetime of the current scope (one per HTTP
/// request in the API, or a fresh one for background/hosted-service scopes
/// that have no HttpContext). Lets independent RecordAsync calls made during
/// the same request be grouped together without threading an id by hand.
/// </summary>
public interface ICorrelationContext
{
    Guid CorrelationId { get; }
}
