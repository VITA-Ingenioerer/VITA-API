namespace Vita.Planning.Application.DTOs;

public sealed class EconomicSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string AppSecretToken { get; set; } = string.Empty;
    public string AgreementGrantToken { get; set; } = string.Empty;
}