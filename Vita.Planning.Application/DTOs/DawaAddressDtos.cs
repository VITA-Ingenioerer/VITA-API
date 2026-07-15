namespace Vita.Planning.Application.DTOs;

public sealed class DawaAddressSearchResultDto
{
    public string Id { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
    public string StreetAddress { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? Municipality { get; set; }
    public string? Region { get; set; }
}

public sealed class DawaPostalCodeDto
{
    public string PostalCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
}

public sealed class DawaRegionDto
{
    public string RegionCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
