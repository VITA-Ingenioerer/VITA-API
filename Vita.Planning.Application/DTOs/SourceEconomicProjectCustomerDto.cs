namespace Vita.Planning.Application.DTOs;

public sealed class SourceEconomicProjectCustomerDto
{
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ObjectVersion { get; set; }
    public string? CorporateIdentificationNumber { get; set; }
}