namespace Vita.Planning.Application.DTOs;

public sealed class VirkCompanySearchDto
{
    public string CvrNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? City { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class VirkCompanyDto
{
    public string CvrNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? AddressLine { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? CountryCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? IndustryCode { get; set; }
    public string? IndustryName { get; set; }
}
